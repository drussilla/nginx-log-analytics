using System;
using NginxLogAnalytics.ContentMatching;
using Xunit;

namespace Test.NginxLogAnalytics
{
    public class TestContentExcludeRule
    {
        [Fact]
        public void Parse_ShouldReturnEqualsRule_IfEqualsRuleProvided()
        {
            var actual = ContentExcludeExcludeRule.Parse("=/test");

            Assert.False(actual.ShouldExcludeFromContent("/tes"));
            Assert.True(actual.ShouldExcludeFromContent("/test"));
            Assert.False(actual.ShouldExcludeFromContent("/test1"));
        }

        [Fact]
        public void Parse_ShouldReturnStartsWithRule_IfStartsWithRuleProvided()
        {
            var actual = ContentExcludeExcludeRule.Parse("^/test");

            Assert.False(actual.ShouldExcludeFromContent("/tes"));
            Assert.True(actual.ShouldExcludeFromContent("/test"));
            Assert.True(actual.ShouldExcludeFromContent("/test1"));
        }

        [Fact]
        public void Parse_ShouldReturnEndsWithRule_IfEndsWithRuleProvided()
        {
            var actual = ContentExcludeExcludeRule.Parse("$test");

            Assert.False(actual.ShouldExcludeFromContent("test1"));
            Assert.True(actual.ShouldExcludeFromContent("test"));
            Assert.True(actual.ShouldExcludeFromContent("1test"));
        }

        [Fact]
        public void Parse_ShouldReturnContainsWithRule_IfContainsWithRuleProvided()
        {
            var actual = ContentExcludeExcludeRule.Parse("~test");

            Assert.False(actual.ShouldExcludeFromContent("t1tet1"));
            Assert.True(actual.ShouldExcludeFromContent("test"));
            Assert.True(actual.ShouldExcludeFromContent("1test"));
            Assert.True(actual.ShouldExcludeFromContent("1test1"));
            Assert.True(actual.ShouldExcludeFromContent("test1"));
        }

        [Fact]
        public void Parse_ShouldThrowFormatException_IfEmptyRuleProvided()
        {
            Assert.Throws<FormatException>(() => ContentExcludeExcludeRule.Parse(""));
        }

        [Fact]
        public void Parse_ShouldThrowFormatException_IfUnknownCheckTypeProvided()
        {
            Assert.Throws<FormatException>(() => ContentExcludeExcludeRule.Parse("%test"));
        }

        [Fact]
        public void Parse_ShouldThrowFormatException_IfEmptyMatchPatternProvided()
        {
            Assert.Throws<FormatException>(() => ContentExcludeExcludeRule.Parse("^"));
        }
    }
}