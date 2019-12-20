using System.Collections.Generic;
using Moq;
using NginxLogAnalytics;
using NginxLogAnalytics.ContentMatching;
using Xunit;

namespace Test.NginxLogAnalytics
{
    public class TestContentValidator
    {
        [Fact]
        public void IsContent_ShouldReturnTrue_IfAllRulesAreNotMatched()
        {
            var rule1 = new Mock<IContentExcludeRule>();
            rule1.Setup(x => x.ShouldExcludeFromContent(It.IsAny<string>())).Returns(false);

            var rule2 = new Mock<IContentExcludeRule>();
            rule2.Setup(x => x.ShouldExcludeFromContent(It.IsAny<string>())).Returns(false);
            var rules = new List<IContentExcludeRule>
            {
                rule1.Object,
                rule2.Object
            };

            ContentMatcher sup = new ContentMatcher(rules);
            var url = "/lol";
            var actual = sup.IsContent(url);
            
            Assert.True(actual);
            rule1.Verify(x => x.ShouldExcludeFromContent(url), Times.AtMostOnce);
            rule2.Verify(x => x.ShouldExcludeFromContent(url), Times.AtMostOnce);
        }

        [Fact]
        public void IsContent_ShouldReturnFalse_IfAtLeastOneRuleIsMatched()
        {
            var rule1 = new Mock<IContentExcludeRule>();
            rule1.Setup(x => x.ShouldExcludeFromContent(It.IsAny<string>())).Returns(false);

            var rule2 = new Mock<IContentExcludeRule>();
            rule2.Setup(x => x.ShouldExcludeFromContent(It.IsAny<string>())).Returns(true);

            var rule3 = new Mock<IContentExcludeRule>();
            rule3.Setup(x => x.ShouldExcludeFromContent(It.IsAny<string>())).Returns(true);
            var rules = new List<IContentExcludeRule>
            {
                rule1.Object,
                rule2.Object,
                rule3.Object
            };
            
            ContentMatcher sup = new ContentMatcher(rules);
            var url = "/lol";
            
            var actual = sup.IsContent(url);
            
            Assert.False(actual);
            rule1.Verify(x => x.ShouldExcludeFromContent(url), Times.AtMostOnce);
            rule2.Verify(x => x.ShouldExcludeFromContent(url), Times.AtMostOnce);
            rule3.Verify(x => x.ShouldExcludeFromContent(url), Times.Never);
        }
    }
}
