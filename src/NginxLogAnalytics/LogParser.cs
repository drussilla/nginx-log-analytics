using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace NginxLogAnalytics
{
    public class LogParser
    {
        private readonly string _path;

        public LogParser(string path)
        {
            _path = path;
        }

        public async Task<List<LogItem>> ParseAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            List<LogItem> items = new List<LogItem>();
            foreach (var line in await File.ReadAllLinesAsync(_path, cancellationToken))
            {
                items.Add(ProcessLine(line));
            }

            return items;
        }

        private LogItem ProcessLine(string line)
        {
            var parts = line.Split('|');

            if (parts.Length != 8)
            {
                throw new FormatException("Wrong log format! It should be '$time_local | $remote_addr | $request | $status | $body_bytes_sent | $http_referer | $http_user_agent | $request_time'");
            }

            var logItem = new LogItem();
            
            logItem.Time = ParseDateTime(parts[0]);
            logItem.RemoteAddress = parts[1].Trim();
            var request = ParseRequest(parts[2]);
            logItem.Method = request.Method;
            logItem.RequestUrl = request.Url;
            logItem.Protocol = request.Protocol;
            logItem.ResponseCode = (HttpStatusCode)ParseInt(parts[3]);
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