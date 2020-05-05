using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NginxLogAnalytics
{
    public class LogParser
    {
        private readonly string _path;
        private readonly Regex _splitRegex = new Regex(" \\| ", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static string[] _crawlerUserAgents;


        public LogParser(string path, string ignoredUserAgentsFilePath)
        {
            _path = path;
            _crawlerUserAgents = File.ReadAllLines(ignoredUserAgentsFilePath);
        }

        public async Task<List<LogItem>> ParseAsync()
        {
            List<LogItem> items = new List<LogItem>();
            var files = Directory.GetFiles(_path, "access.log*", SearchOption.TopDirectoryOnly);

            await Task.WhenAll(
                from partition in Partitioner.Create(files).GetPartitions(Environment.ProcessorCount)
                select Task.Run(async delegate
                {
                    using (partition)
                    {
                        while (partition.MoveNext())
                        {
                            await ProcessFile(partition.Current, items);
                        }
                    }
                }));

            return items;
        }

        private async Task ProcessFile(string file, List<LogItem> items)
        {
            Console.WriteLine($"Processing file {Path.GetFileName(file)}...");
            foreach (var line in await File.ReadAllLinesAsync(file))
            {
                items.Add(ProcessLine(line));
            }
        }

        private LogItem ProcessLine(string line)
        {
            var parts = _splitRegex.Split(line);

            if (parts.Length != 8)
            {
                throw new FormatException("Wrong log format! It should be '$time_local | $remote_addr | $request | $status | $body_bytes_sent | $http_referer | $http_user_agent | $request_time'");
            }

            var logItem = new LogItem();

            var responseCode = ParseInt(parts[3]);
            if (responseCode == 0)
            {
                // broken request
                // https://stackoverflow.com/questions/9791684/what-is-http-status-code-000
                // https://www.reddit.com/r/nginx/comments/73so8r/access_log_shows_http_response_code_000_with_0/dnul7da/
                
                logItem.Method = "-";
                logItem.Protocol = "-";
                logItem.RequestUrl = "-";
                responseCode = 500;
            }
            else if (!string.IsNullOrWhiteSpace(parts[2]))
            {
                var request = ParseRequest(parts[2]);
                logItem.Method = request.Method;
                logItem.RequestUrl = request.Url;
                logItem.Protocol = request.Protocol;
            }

            logItem.Time = ParseDateTime(parts[0]);
            logItem.RemoteAddress = parts[1].Trim();
            
            logItem.ResponseCode = (HttpStatusCode)responseCode;
            logItem.BodySentBytes = ParseInt(parts[4]);
            logItem.Referrer = parts[5].Trim();
            logItem.UserAgent = parts[6].Trim();
            logItem.RequestTime = ParseFloat(parts[7]);
            
            var ignoreCheckResult = IsInIgnoreList(logItem.UserAgent);
            logItem.ShouldIgnore = ignoreCheckResult.isIgnored;
            logItem.IgnoreMatch = ignoreCheckResult.match;

            return logItem;
        }

        private static (bool isIgnored, string match) IsInIgnoreList(string userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
            {
                return (true, null);
            }

            if (userAgent == "-")
            {
                return (true, "-");
            }

            for (int i = 0; i < _crawlerUserAgents.Length; i++)
            {
                if (userAgent.IndexOf(_crawlerUserAgents[i], StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return (true, _crawlerUserAgents[i]);
                }
            }

            return (false, null);
        }

        private static int ParseInt(string str)
        {
            var trimmed = str.Trim();
            if (!int.TryParse(trimmed, out var result))
            {
                throw new FormatException("Wrong integer format. Expecting integer value but got " + trimmed);
            }

            return result;
        }

        private static float ParseFloat(string str)
        {
            var trimmed = str.Trim();
            if (!float.TryParse(trimmed, out var result))
            {
                throw new FormatException("Wrong float format. Expecting integer value but got " + trimmed);
            }

            return result;
        }

        private (string Method, string Url, string Protocol) ParseRequest(string request)
        {
            var trimmed = request.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return (null, null, null);
            }

            var firstSpace = trimmed.IndexOf(' ');
            var lastSpace = trimmed.LastIndexOf(' ');
            if (firstSpace == -1 && lastSpace == -1)
            {
                return (null, null, null);
            }

            if (firstSpace >= lastSpace ||
                firstSpace == 0 || 
                lastSpace == trimmed.Length - 1)
            {
                throw new FormatException($"Wrong request format! Should be the following: 'Method Url Protocol', but got: '{request}'" );
            }

            var method = trimmed.Substring(0, firstSpace);
            var url = trimmed.Substring(firstSpace + 1, lastSpace - firstSpace - 1);
            var protocol = trimmed.Substring(lastSpace + 1, trimmed.Length - lastSpace - 1);

            return (method.Trim(), WebUtility.UrlDecode(url.Trim()), protocol.Trim());
        }

        private static DateTime ParseDateTime(string timeString)
        {
            if (!DateTime.TryParseExact(timeString.Trim(), "dd/MMM/yyyy:HH:mm:ss K", CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal,
                out var time))
            {
                throw new FormatException("Wrong date time format! Expecting the following format: 'dd/MMM/yyyy:HH:mm:ss K'");
            }

            return time;
        }
    }
}