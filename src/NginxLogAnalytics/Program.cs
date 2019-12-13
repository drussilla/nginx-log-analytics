using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace NginxLogAnalytics
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var config = Config.Load("config.json");

            _crawlerUserAgents = File.ReadAllLines(config.CrawlerUserAgentsFilePath);
            
            var parser = new LogParser(config.LogFilesFolderPath);
            var items = parser.ParseAsync(CancellationToken.None).Result;

            if (args.Length == 1)
            {
                ShowUrlDetails(items, args[0]);
                return;
            }

            var contentNotCrawlers = items
                .Where(x => 
                    !IsCrawler(x)
                    && IsPages(x) 
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

            Console.WriteLine("-- Today's Top 10 --");
            var result = contentNotCrawlers
                .Where(x => x.Time.Date == DateTime.UtcNow.Date)
                .GroupBy(x => x.NormalizedRequestUrl)
                .Select(x => new { Url = x.Key, Count = x.Count(), LogItems = x})
                .OrderByDescending(x => x.Count)
                .Take(10);

            foreach (var item in result)
            {
                Console.WriteLine($"{item.Count} - {item.Url}");
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

            var groupByReferrer = notCrawlers.GroupBy(x => x.Referrer)
                .Select(x => new {Referrer = x.Key, Count = x.Count()})
                .OrderByDescending(x => x.Count)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("-- Referrers --");
            foreach (var item in groupByReferrer)
            {
                Console.WriteLine($"{item.Count} \t {item.Referrer}");
            }

            var groupByUserAgents = notCrawlers.GroupBy(x => x.UserAgent)
                .Select(x => new {UserAgent = x.Key, Count = x.Count()})
                .OrderByDescending(x => x.Count)
                .ToList();

            Console.WriteLine();
            Console.WriteLine("-- User Agents --");
            foreach (var item in groupByUserAgents)
            {
                Console.WriteLine($"{item.Count} \t {item.UserAgent}");
            }

            var groupCrawlersByUserAgents = crawlers.GroupBy(x => x.UserAgent)
                .Select(x => new {UserAgent = x.Key, Count = x.Count()})
                .OrderByDescending(x => x.Count)
                .ToList();

            Console.WriteLine();
            WriteLine("-- User Agents (Crawlers) --", ConsoleColor.DarkYellow);
            foreach (var item in groupCrawlersByUserAgents)
            {
                Console.WriteLine($"{item.Count} \t {item.UserAgent}");
            }
        }

        private static void Referrers(List<LogItem> contentNotCrawlers)
        {
            Console.WriteLine("-- Referrers --");
            var referrers = contentNotCrawlers
                .GroupBy(x => x.Referrer)
                .Select(x => new {Referrer = x.Key, Count = x.Count()})
                .OrderByDescending(x => x.Count).ToList();

            foreach (var referrer in referrers)
            {
                Console.WriteLine($"{referrer.Count} \t {referrer.Referrer}");
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
                .Where(x => x.Time >= DateTime.UtcNow.AddDays(-7))
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

        private static bool IsPages(LogItem logItem)
        {
            return !logItem.NormalizedRequestUrl.Equals("/feed", StringComparison.OrdinalIgnoreCase) &&
                   !logItem.NormalizedRequestUrl.StartsWith("/wp-content") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/favicon") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/site.webmanifest") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/comment.aspx") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/comment-submitted") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/fonts/") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/robots.txt") &&
                   !logItem.NormalizedRequestUrl.EndsWith(".png") &&
                   !logItem.NormalizedRequestUrl.EndsWith(".svg") &&
                   !logItem.NormalizedRequestUrl.EndsWith(".jpg")
                ;
        }

        private static string[] _crawlerUserAgents;

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
