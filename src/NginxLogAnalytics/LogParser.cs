using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

namespace NginxLogAnalytics
{
    public class LogParser
    {
        private readonly string _path;
        private readonly Regex _splitRegex = new Regex(" \\| ", RegexOptions.Compiled);

        public LogParser(string path)
        {
            _path = path;
        }

        public List<LogItem> Parse()
        {
            List<LogItem> items = new List<LogItem>();
            var files = Directory.GetFiles(_path, "access.log*", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                Console.WriteLine($"Processing file {Path.GetFileName(file)}...");
                foreach (var line in File.ReadAllLines(file))
                {
                    items.Add(ProcessLine(line));
                }
            }

            return items;
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
                //https://stackoverflow.com/questions/9791684/what-is-http-status-code-000
                // https://www.reddit.com/r/nginx/comments/73so8r/access_log_shows_http_response_code_000_with_0/dnul7da/
                
                logItem.Method = "-";
                logItem.Protocol = "-";
                logItem.RequestUrl = "-";
                responseCode = 500;
            }
            else
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

            return logItem;
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
            var parts = request.Trim().Split(' ');
            if (parts.Length != 3)
            {
                throw new FormatException("Wrong request format! Should be the following: 'Method Url Protocol'");
            }

            return (parts[0].Trim(), WebUtility.UrlDecode(parts[1].Trim()), parts[2].Trim());
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