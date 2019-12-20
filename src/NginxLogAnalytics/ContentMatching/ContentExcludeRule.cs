using System;

namespace NginxLogAnalytics.ContentMatching
{
    public class ContentExcludeExcludeRule : IContentExcludeRule
    {
        private readonly Predicate<string> _check;
        private ContentExcludeExcludeRule(Func<string, string, bool> check, string matchPattern)
        {
            _check = x => check(x, matchPattern);
        }

        public bool ShouldExcludeFromContent(string url)
        {
            return _check(url);
        }

        public static IContentExcludeRule Parse(string rule)
        {
            if (string.IsNullOrWhiteSpace(rule))
            {
                throw new FormatException("Rule cannot be empty.");
            }

            if (rule.Length < 2)
            {
                throw new FormatException("Rule should have check type and match pattern.");
            }

            var checkType = rule[0];

            var matchPattern = rule.Substring(1);
            if (string.IsNullOrWhiteSpace(matchPattern))
            {
                throw new FormatException("Rule cannot have an empty not whitespace match pattern.");
            }

            return checkType switch
            {
                '=' => new ContentExcludeExcludeRule((url, match) => url.Equals(match, StringComparison.OrdinalIgnoreCase), matchPattern),
                '^' => new ContentExcludeExcludeRule((url, match) => url.StartsWith(match, StringComparison.OrdinalIgnoreCase), matchPattern),
                '$' => new ContentExcludeExcludeRule((url, match) => url.EndsWith(match, StringComparison.OrdinalIgnoreCase), matchPattern),
                '~' => new ContentExcludeExcludeRule((url, match) => url.Contains(match, StringComparison.OrdinalIgnoreCase), matchPattern),
                _ => throw new FormatException($"Rule has unknown check type {checkType}. Supported types: =, ^, $, ~")
            };
        }
    }
}