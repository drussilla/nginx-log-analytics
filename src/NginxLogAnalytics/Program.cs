﻿using System;
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
        static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;
            var config = Config.Load("config.json");

            _crawlerUserAgents = File.ReadAllLines(config.CrawlerUserAgentsFilePath);
            
            var parser = new LogParser(config.LogFilesFolderPath);
            var items = parser.ParseAsync(CancellationToken.None).Result;

            var contentNotCrawlers = items
                .Where(x => !IsCrawler(x) && IsPages(x) && (int)x.ResponseCode < 400 && x.ResponseCode != HttpStatusCode.Moved).ToList();

            var notFoundItems = items
                .Count(x => x.ResponseCode == HttpStatusCode.NotFound);
            var crawlers = items.Count(IsCrawler);

            Console.WriteLine($"Total: {items.Count}");
            Console.WriteLine($"Crawlers: {crawlers}");
            Console.WriteLine($"404: {notFoundItems}");
            Console.WriteLine($"Content: {contentNotCrawlers.Count}");

            Console.WriteLine();

            Today(contentNotCrawlers);

            ShowLastSevenDays(contentNotCrawlers);

            ShowByWeek(contentNotCrawlers);

            Console.WriteLine("-- Today --");
            var result = contentNotCrawlers
                .Where(x => x.Time.Date == DateTime.UtcNow.Date)
                .GroupBy(x => x.NormalizedRequestUrl)
                .Select(x => new { Url = x.Key, Count = x.Count(), LogItems = x})
                .OrderByDescending(x => x.Count);

            foreach (var item in result)
            {
                Console.WriteLine($"{item.Count} - {item.Url}");
            }
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
