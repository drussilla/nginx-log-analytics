namespace NginxLogAnalytics.Utils
{
    public interface IFileSystem
    {
        string[] ReadAllLines(string path);
    }
}