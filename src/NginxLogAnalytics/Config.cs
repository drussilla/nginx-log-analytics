using System;

namespace NginxLogAnalytics
{
    public class Config
    {
        public string LogFilesFolderPath { get; set; }
        public string CrawlerUserAgentsFilePath { get; set; }
        public string ExcludeFromContentFilePath { get; set; }
        public DateTime? Date { get; set; }
        public string Url { get; set; }
    }
}