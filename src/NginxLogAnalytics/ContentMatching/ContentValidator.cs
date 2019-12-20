using System.Collections.Generic;

namespace NginxLogAnalytics.ContentMatching
{
    public class ContentMatcher
    {
        private readonly List<IContentExcludeRule> _rules;

        public ContentMatcher(List<IContentExcludeRule> rules)
        {
            _rules = rules;
        }

        public bool IsContent(string url)
        {
            foreach (var rule in _rules)
            {
                if (rule.ShouldExcludeFromContent(url))
                {
                    return false;
                }
            }

            return true;
        }
    }
}