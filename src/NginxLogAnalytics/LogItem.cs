using System;
using System.Net;

namespace NginxLogAnalytics
{
    public class LogItem
    {
        public DateTime Time { get; set; }
        public string RemoteAddress { get; set; }
        public string Method { get; set; }
        public string RequestUrl { get; set; }
        public string Protocol { get; set; }
        public HttpStatusCode ResponseCode { get; set; }
        public int BodySentBytes { get; set; }
        public string Referrer { get; set; }
        public string UserAgent { get; set; }
        public float RequestTime { get; set; }

        private string _normalizedRequestUrl;
        public string NormalizedRequestUrl
        {
            get { return _normalizedRequestUrl ??= Normalize(RequestUrl); }
        }

        public bool ShouldIgnore { get; set; }
        public string IgnoreMatch { get; set; }

        private string Normalize(string requestUrl)
        {
            if (string.IsNullOrWhiteSpace(requestUrl))
            {
                return requestUrl;
            }

            var index = requestUrl.IndexOf('?');
            if (index != -1)
            {
                requestUrl = requestUrl.Substring(0, index);
            }

            index = requestUrl.IndexOf('#');
            if (index != -1)
            {
                requestUrl = requestUrl.Substring(0, index);
            }

            if (requestUrl.Length > 1)
            {
                requestUrl = requestUrl.TrimEnd('/');
            }
            
            return requestUrl;
        }
    }
}