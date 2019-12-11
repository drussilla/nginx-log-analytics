using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
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
            
            var parser = new LogParser(config.LogFilePath);
            var items = parser.ParseAsync(CancellationToken.None).Result;

            var contentNotCrawlers = items
                .Where(x => !IsCrawler(x) && IsPages(x) && (int)x.ResponseCode < 400 && x.ResponseCode != HttpStatusCode.Moved).ToList();

            Console.WriteLine($"Total: {items.Count}");
            Console.WriteLine($"Filtered: {contentNotCrawlers.Count}");

            var result = contentNotCrawlers.GroupBy(x => x.NormalizedRequestUrl)
                .Select(x => new { Url = x.Key, Count = x.Count(), OkCOunt = x.Count(y => (int)y.ResponseCode >= 200 && (int)y.ResponseCode < 300), LogItems = x})
                .Where(x => x.Count > 1)
                .OrderByDescending(x => x.Count);

            foreach (var item in result)
            {
                Console.WriteLine($"{item.Count} - {item.OkCOunt} - {item.Url}");
            }

            //var notFoundItems = items.Where(x => x.ResponseCode == HttpStatusCode.NotFound).ToList();

            //var crawlers = items.Where(IsCrawler).ToList();

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
