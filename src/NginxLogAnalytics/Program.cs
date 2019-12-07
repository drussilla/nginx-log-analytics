using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace NginxLogAnalytics
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Please specify access.log file path");
                return;
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"Specify file {args[0]} doesn't exist.");
                return;
            }

            Console.OutputEncoding = Encoding.UTF8;
            var parser = new LogParser(args[0]);
            var items = parser.ParseAsync(CancellationToken.None).Result;

            var filteredItems = items
                .Where(x => !IsCrawler(x) && IsPages(x) && x.ResponseCode != HttpStatusCode.NotFound).ToList();

            Console.WriteLine($"Total: {items.Count}");
            Console.WriteLine($"Filtered: {filteredItems.Count}");

            var result = filteredItems.GroupBy(x => x.NormalizedRequestUrl)
                .Select(x => new { Url = x.Key, Count = x.Count(), LogItems = x})
                .OrderByDescending(x => x.Count);

            foreach (var item in result)
            {
                Console.WriteLine(item.Count + "\t\t" + item.Url);
                    foreach (var userAgent in item.LogItems.Select(x => x.UserAgent).Distinct())
                    {
                        Console.WriteLine($"\t{userAgent}");
                    }
            }

        }

        private static bool IsPages(LogItem logItem)
        {
            return !logItem.NormalizedRequestUrl.Equals("/feed", StringComparison.OrdinalIgnoreCase) &&
                   !logItem.NormalizedRequestUrl.StartsWith("/wp-content") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/favicon") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/site.webmanifest") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/comment.aspx") &&
                   !logItem.NormalizedRequestUrl.StartsWith("/fonts/") &&
                   !logItem.NormalizedRequestUrl.EndsWith(".png") &&
                   !logItem.NormalizedRequestUrl.EndsWith(".svg") &&
                   !logItem.NormalizedRequestUrl.EndsWith(".jpg")
                ;
        }

        private static bool IsCrawler(LogItem logItem)
        {
            return logItem.UserAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
                   logItem.UserAgent.Contains("feed", StringComparison.OrdinalIgnoreCase);
        }
    }
}
