using System;
using System.Collections.Generic;
using System.IO;
using NginxLogAnalytics.Utils;

namespace NginxLogAnalytics.ContentMatching
{
    public class ContentExcludeListParser
    {
        private readonly IFileSystem _fileSystem;

        public ContentExcludeListParser(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public List<IContentExcludeRule> Parse(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Cannot find {path} file.");
            }

            var rules = new List<IContentExcludeRule>();
            var lines = _fileSystem.ReadAllLines(path);
            var line = 0;
            foreach (var rule in lines)
            {
                line++;
                if (string.IsNullOrWhiteSpace(rule))
                {
                    // ignore empty lines
                    continue;
                }

                if (rule.StartsWith("//"))
                {
                    // ignore comments
                    continue;
                }

                try
                {
                    rules.Add(ContentExcludeExcludeRule.Parse(rule));
                }
                catch (FormatException e)
                {
                    Console.WriteLine($"{e.Message} line {line}");
                    throw;
                }
            }

            return rules;
        }
    }
}