using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using NginxLogAnalytics.ContentMatching;
using NginxLogAnalytics.Utils;

namespace NginxLogAnalytics
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var config = Config.Load("config.json");

            var excludeContentListParser = new ContentExcludeListParser(new FileSystem());
            _contentMatcher = new ContentMatcher(excludeContentListParser.Parse(config.ExcludeFromContentFilePath));

            _crawlerUserAgents = File.ReadAllLines(config.CrawlerUserAgentsFilePath);
            
            var parser = new LogParser(config.LogFilesFolderPath);
            var items = parser.Parse();

            if (args.Length >= 1)
            {
                if (args.Length >= 2)
                {
                    try
                    {
                        var dateLimit = DateTime.Parse(args[1]);
                        Console.WriteLine($"Data limited to {dateLimit.Date:yyyy-MM-dd}");
                        items = items.Where(x => x.Time.Date == dateLimit.Date).ToList();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(
                            $"Error: Please specify date as a second parameter in the yyyy-MM-dd format. Details: {e.Message}");
                        return;
                    }
                    
                }

                ShowUrlDetails(items, args[0]);
                return;
            }

            var contentNotCrawlers = items
                .Where(x => 
                    !IsCrawler(x)
                    && IsContent(x) 
                    && (int)x.ResponseCode < 400 && x.ResponseCode != HttpStatusCode.Moved
                    && x.Referrer != "https://druss.co/xmlrpc.php").ToList();

            var notFoundItems = items
                .Count(x => x.ResponseCode == HttpStatusCode.NotFound);
            var crawlers = items.Count(IsCrawler);

            Console.Write($"Total: {items.Count} Crawlers: ");
            Write(crawlers.ToString(), ConsoleColor.DarkYellow);
            Console.Write(" 404: ");
            Write(notFoundItems.ToString(), ConsoleColor.DarkRed);
            Console.Write(" Content: ");
            WriteLine(contentNotCrawlers.Count.ToString(), ConsoleColor.DarkGreen);

            Console.WriteLine();

            Today(contentNotCrawlers);

            ShowLastSevenDays(contentNotCrawlers);

            ShowByWeek(contentNotCrawlers);

            Console.WriteLine("-- Today's Top 15 --");
            var result = contentNotCrawlers
                .Where(x => x.Time.Date == DateTime.UtcNow.Date)
                .GroupBy(x => x.NormalizedRequestUrl)
                .Select(x => new { Url = x.Key, Count = x.Count(), LogItems = x})
                .OrderByDescending(x => x.Count)
                .Take(15);

            foreach (var item in result)
            {
                Console.WriteLine($"{item.Count} \t {item.Url}");
            }

            Console.WriteLine();
            //Referrers(contentNotCrawlers);
        }

        private static void ShowUrlDetails(List<LogItem> items, string url)
        {
            Console.WriteLine();
            Console.WriteLine("Details: " + url);
            var filtered = items.Where(x => x.NormalizedRequestUrl.Equals(url, StringComparison.OrdinalIgnoreCase)).ToList();

            var crawlers = filtered.Where(IsCrawler).ToList();
            var notCrawlers = filtered.Where(x => !IsCrawler(x)).ToList();
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

        private static void Today(List<LogItem> contentNotCrawlers)
        {
            var todayItems = contentNotCrawlers.Count(x => x.Time.Date == DateTime.UtcNow.Date);
            var yesterday = DateTime.UtcNow.AddDays(-1);
            var equivalentYesterdayItems = contentNotCrawlers.Count(x => x.Time >= yesterday.Date && x.Time <= yesterday);
            Console.Write($"-- Today: {todayItems} ");

            var diff = todayItems - equivalentYesterdayItems;
            var defaultColor = Console.ForegroundColor;
            Console.ForegroundColor = diff > 0 ? ConsoleColor.DarkGreen : ConsoleColor.DarkRed;
            Console.Write((diff > 0 ? "+" : "") + diff);
            Console.ForegroundColor = defaultColor;
            Console.WriteLine($" ({equivalentYesterdayItems})");
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

        private static void ShowByWeek(List<LogItem> items)
        {
            Console.WriteLine("-- By weeks --");

            var weeks = items
                .GroupBy(x =>
                    CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(x.Time, CalendarWeekRule.FirstFourDayWeek,
                        DayOfWeek.Monday))
                .OrderBy(x => x.Key)
                .Select(x => (title: x.Key.ToString(), count: x.Count()))
                .ToList();

            PrintChart(weeks);

            Console.WriteLine();
        }

        private static void PrintChart(List<(string title, int count)> weeks)
        {
            var maxBarLength = 80f;
            var barChar = '▄';
            var maxValue = weeks.Max(x => x.count);
            var viewsPerSquare = Math.Ceiling(maxValue / maxBarLength);
            foreach (var day in weeks)
            {
                Console.Write($"{day.title} ");
                var squares = (int) Math.Ceiling(day.count / viewsPerSquare);
                Console.WriteLine($"{new string(barChar, squares)} {day.count}");
            }
        }

        private static bool IsContent(LogItem logItem)
        {
            return _contentMatcher.IsContent(logItem.NormalizedRequestUrl);
        }

        private static string[] _crawlerUserAgents;
        private static ContentMatcher _contentMatcher;

        private static bool IsCrawler(LogItem logItem)
        {
            if (logItem.UserAgent == "-")
            {
                return true;
            }

            for (int i = 0; i < _crawlerUserAgents.Length; i++)
            {
                if (logItem.UserAgent.Contains(_crawlerUserAgents[i], StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
