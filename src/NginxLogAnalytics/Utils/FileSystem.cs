using System.IO;

namespace NginxLogAnalytics.Utils
{
    public class FileSystem : IFileSystem
    {
        public string[] ReadAllLines(string path)
        {
            return File.ReadAllLines(path);
        }
    }
}