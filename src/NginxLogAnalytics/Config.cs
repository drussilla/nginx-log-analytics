using System.IO;
using System.Text.Json;

namespace NginxLogAnalytics
{
    public class Config
    {
        public string LogFilesFolderPath { get; set; }
        public string CrawlerUserAgentsFilePath { get; set; }
        public string ExcludeContentFilePath { get; set; }

        public static Config Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Config doesn't exists at {path}.");
            }

            return JsonSerializer.Deserialize<Config>(File.ReadAllText(path));
        }
    }
}