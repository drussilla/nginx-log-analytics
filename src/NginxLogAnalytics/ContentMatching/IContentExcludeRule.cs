namespace NginxLogAnalytics.ContentMatching
{
    public interface IContentExcludeRule
    {
        bool ShouldExcludeFromContent(string url);
    }
}