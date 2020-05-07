using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NginxLogAnalytics.ContentMatching;
using NginxLogAnalytics.Utils;

namespace NginxLogAnalytics
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            Stopwatch elapsedParsing = new Stopwatch();
            Stopwatch elapsedProcessing = new Stopwatch();

            elapsedParsing.Start();
            
            var configuration = new ConfigurationBuilder();
            configuration.AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "config.json"), false, false);
            configuration.AddCommandLine(args);
            
            var config = configuration.Build().Get<Config>();
            
            var excludeContentListParser = new ContentExcludeListParser(new FileSystem());
            _contentMatcher = new ContentMatcher(excludeContentListParser.Parse(config.ExcludeFromContentFilePath));

            var parser = new LogParser(config.LogFilesFolderPath, config.CrawlerUserAgentsFilePath);
            var items = await parser.ParseAsync();
            elapsedParsing.Stop();

            if (!string.IsNullOrWhiteSpace(config.Url))
            {
                if (config.Date != null)
                {
                    try
                    {
                        Console.WriteLine($"Data limited to {config.Date.Value.Date:yyyy-MM-dd}");
                        items = items.Where(x => x.Time.Date == config.Date.Value.Date).ToList();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(
                            $"Error: Please specify date as a second parameter in the yyyy-MM-dd format. Details: {e.Message}");
                        return;
                    }
                }

                ShowUrlDetails(items, config.Url);
                return;
            }

            elapsedProcessing.Start();
            //IgnoreStatistics(items);
            //return;
            var contentNotCrawlers = items
                .Where(x => 
                    !x.ShouldIgnore
                    && IsContent(x) 
                    && (int)x.ResponseCode < 400 && x.ResponseCode != HttpStatusCode.Moved
                    && x.Referrer != "https://druss.co/xmlrpc.php")
                .ToList();

            var notFoundItems = items
                .Count(x => x.ResponseCode == HttpStatusCode.NotFound);
            var crawlers = items.Count(x => x.ShouldIgnore);

            Console.Write($"Total: {items.Count} Crawlers: ");
            Write(crawlers.ToString(), ConsoleColor.DarkYellow);
            Console.Write(" 404: ");
            Write(notFoundItems.ToString(), ConsoleColor.DarkRed);
            Console.Write(" Content: ");
            WriteLine(contentNotCrawlers.Count.ToString(), ConsoleColor.DarkGreen);

            Console.WriteLine();

            ShowLastSevenDays(contentNotCrawlers);

            ShowLastSevenWeeks(contentNotCrawlers);

            var date = DateTime.UtcNow;
            if (config.Date.HasValue)
            {
                date = config.Date.Value.AddHours(23).AddMinutes(59).AddSeconds(59).AddMilliseconds(999);
            }

            DayStats(date, contentNotCrawlers);

            Console.WriteLine($"-- {date:yyyy-MM-dd}'s Top 15 --");
            var result = contentNotCrawlers
                .Where(x => x.Time.Date == date.Date)
                .GroupBy(x => x.NormalizedRequestUrl)
                .Select(x => new { Url = x.Key, Count = x.Count(), LogItems = x})
                .OrderByDescending(x => x.Count)
                .Take(15);

            foreach (var item in result)
            {
                Console.WriteLine($"{item.Count} \t {item.Url}");
            }

            elapsedProcessing.Stop();

            Console.WriteLine();
            Console.WriteLine("Elapsed parsing: " + elapsedParsing.Elapsed.TotalSeconds + "s");
            Console.WriteLine("Elapsed processing: " + elapsedProcessing.Elapsed.TotalSeconds + "s");
            Console.WriteLine("Total: " + (elapsedParsing.Elapsed.TotalSeconds + elapsedProcessing.Elapsed.TotalSeconds) + "s");
            //Referrers(contentNotCrawlers);
        }

        private static void IgnoreStatistics(List<LogItem> items)
        {
            // Check what is the most ignored items, 
            // update list in the same order to get the best performance
            var ignored = items.Where(x => x.ShouldIgnore)
                .GroupBy(x => x.IgnoreMatch)
                .Select(x => new {Match = x.Key, Total = x.Count(), SubMatch = x.GroupBy(y => y.UserAgent).Select(y => new { Total = y.Count(), Mathc = y.Key})})
                .OrderByDescending(x => x.Total);
            foreach (var item in ignored)
            {
                Console.WriteLine($"{item.Total}\t\t{item.Match}");
                foreach (var subItem in item.SubMatch.OrderByDescending(x => x.Total))
                {
                    Console.WriteLine($"--{subItem.Total}\t\t{subItem.Mathc}");
                }
            }
        }

        private static void ShowUrlDetails(List<LogItem> items, string url)
        {
            Console.WriteLine();
            Console.WriteLine("Details: " + url);
            var filtered = items.Where(x => x.NormalizedRequestUrl?.Equals(url, StringComparison.OrdinalIgnoreCase) ?? false).ToList();

            var crawlers = filtered.Where(x => x.ShouldIgnore).ToList();
            var notCrawlers = filtered.Where(x => !x.ShouldIgnore).ToList();
            Console.Write($"Total: {filtered.Count}");
            Write($" Crawlers: {crawlers.Count}", ConsoleColor.DarkYellow);
            Write($" Users: {notCrawlers.Count}", ConsoleColor.DarkGreen);

            var countPerResponseCode = notCrawlers.GroupBy(x => x.ResponseCode)
                .Select(x => new {Response = x.Key, Count = x.Count()})
                .OrderByDescending(x => x.Count)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("-- Response codes --");
            foreach (var item in countPerResponseCode)
            {
                var color = ConsoleColor.DarkGreen;
                if ((int) item.Response >= 300)
                {
                    color = ConsoleColor.DarkYellow;
                } 
                if ((int) item.Response >= 400)
                {
                    color = ConsoleColor.DarkCyan;
                }
                if ((int) item.Response >= 500)
                {
                    color = ConsoleColor.DarkRed;
                }

                Write($"{(int)item.Response}", color);
                Console.Write($" {item.Count}; ");
            }
            Console.WriteLine();
            
            DisplayGroupBy(notCrawlers, x => x.Referrer, "Referrers", ConsoleColor.DarkGreen);
            DisplayGroupBy(notCrawlers, x => x.RemoteAddress, "IP Address", ConsoleColor.DarkGreen);
            DisplayGroupBy(notCrawlers, x => x.UserAgent, "User Agents", ConsoleColor.DarkGreen);
            DisplayGroupBy(crawlers, x => x.UserAgent, "User Agents (Crawlers)", ConsoleColor.DarkYellow);
        }

        private static void DisplayGroupBy(List<LogItem> items, Func<LogItem, object> groupBy, string name, ConsoleColor color)
        {
            var groupCrawlersByUserAgents = items.GroupBy(groupBy)
                .Select(x => new { Item = x.Key, Count = x.Count() })
                .OrderByDescending(x => x.Count)
                .ToList();

            Console.WriteLine();
            WriteLine($"-- {name} --", color);
            foreach (var item in groupCrawlersByUserAgents)
            {
                Console.WriteLine($"{item.Count} \t {item.Item}");
            }
        }

        private static void Write(string text, ConsoleColor color)
        {
            var defColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ForegroundColor = defColor;
        }

        private static void WriteLine(string text, ConsoleColor color)
        {
            var defColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = defColor;
        }

        private static void DayStats(DateTime day, List<LogItem> contentNotCrawlers)
        {
            var dayItems = contentNotCrawlers.Count(x => x.Time.Date == day.Date);
            var theDayBefore = day.AddDays(-1);
            var equivalentTheDayBeforeItems = contentNotCrawlers.Count(x => x.Time >= theDayBefore.Date && x.Time <= theDayBefore);
            Console.Write($"-- {day:yyyy-MM-dd}: {dayItems} ");

            var diff = dayItems - equivalentTheDayBeforeItems;
            var defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = diff > 0 ? ConsoleColor.DarkGreen : ConsoleColor.DarkRed;
            Console.Write((diff > 0 ? "+" : "") + diff);
            Console.ForegroundColor = defaultColor;
            Console.WriteLine($" ({equivalentTheDayBeforeItems})");
            Console.WriteLine();
        }

        private static void ShowLastSevenDays(List<LogItem> items)
        {
            Console.WriteLine("-- Last 7 days --");
            var last7Days = items
                .Where(x => x.Time.Date >= DateTime.UtcNow.AddDays(-6).Date)
                .GroupBy(x => x.Time.Date)
                .OrderBy(x => x.Key)
                .Select(x =>  (x.Key.ToString("MM-dd"), x.Count()))
                .ToList();

            PrintChart(last7Days);

            Console.WriteLine();
        }

        private static void ShowLastSevenWeeks(List<LogItem> items)
        {
            Console.WriteLine("-- Last 7 weeks --");
            
            var now = DateTime.UtcNow;
            var sixWeeksAgo = now.AddDays(-7 * 7);
            var dayOfWeekSixWeeksAgo = CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(sixWeeksAgo);
            var daysToMonday = dayOfWeekSixWeeksAgo - DayOfWeek.Monday;
            if (daysToMonday == -1) // DayOfWeek starts from Sunday = 0
            {
                daysToMonday = 7;
            }

            var firstDayOfTheFirstWeek = new DateTime(sixWeeksAgo.Year, sixWeeksAgo.Month,
                sixWeeksAgo.Day - daysToMonday, CultureInfo.InvariantCulture.Calendar);

            var last7Weeks = items
                .Where(x => x.Time.Date >= firstDayOfTheFirstWeek)
                .GroupBy(x =>
                    CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(x.Time, CalendarWeekRule.FirstFourDayWeek,
                        DayOfWeek.Monday))
                .OrderBy(x => x.Key)
                .Select(x => (title: x.Key.ToString(), count: x.Count()))
                .ToList();

            PrintChart(last7Weeks);

            Console.WriteLine();
        }

        private static void PrintChart(List<(string title, int count)> items)
        {
            var maxBarLength = 80f;
            var barChar = '▄';
            var maxValue = items.Max(x => x.count);
            var viewsPerSquare = Math.Ceiling(maxValue / maxBarLength);
            foreach (var item in items)
            {
                Console.Write($"{item.title} ");
                var squares = (int) Math.Ceiling(item.count / viewsPerSquare);
                Console.WriteLine($"{new string(barChar, squares)} {item.count}");
            }
        }

        private static bool IsContent(LogItem logItem)
        {
            return _contentMatcher.IsContent(logItem.NormalizedRequestUrl);
        }

        
        private static ContentMatcher _contentMatcher;
    }
}
