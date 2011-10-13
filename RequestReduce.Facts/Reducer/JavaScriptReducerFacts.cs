﻿using System;
using System.Collections.Generic;
using System.Text;
using Moq;
using RequestReduce.Configuration;
using RequestReduce.Reducer;
using RequestReduce.Store;
using RequestReduce.Utilities;
using Xunit;
using UriBuilder = RequestReduce.Utilities.UriBuilder;
using RequestReduce.Module;
using RequestReduce.ResourceTypes;
using System.Net;
using System.IO;
using Xunit.Extensions;

namespace RequestReduce.Facts.Reducer
{
    public class JavaScriptReducerFacts
    {
        private class TestableJavaScriptReducer : Testable<RequestReduce.Reducer.JavaScriptReducer>
        {
            public TestableJavaScriptReducer()
            {
                Mock<IMinifier>().Setup(x => x.Minify<JavaScriptResource>(It.IsAny<string>())).Returns("minified");
                Mock<ISpriteManager>().Setup(x => x.GetEnumerator()).Returns(new List<SpritedImage>().GetEnumerator());
                Inject<IUriBuilder>(new UriBuilder(Mock<IRRConfiguration>().Object));
                Mock<IRRConfiguration>().Setup(x => x.JavaScriptUrlsToIgnore).Returns(string.Empty);
                var mockWebResponse = new Mock<WebResponse>();
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection());
                Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>(It.IsAny<string>())).Returns(mockWebResponse.Object);
            }

        }

        public class SupportedResourceType
        {
            [Fact]
            public void WillSupportJavaScript()
            {
                var testable = new TestableJavaScriptReducer();

                Assert.Equal(typeof(JavaScriptResource), testable.ClassUnderTest.SupportedResourceType);
            }
        }


        public class Process
        {
            [Fact]
            public void WillReturnProcessedJsUrlInCorrectConfigDirectory()
            {
                var testable = new TestableJavaScriptReducer();
                testable.Mock<IRRConfiguration>().Setup(x => x.SpriteVirtualPath).Returns("spritedir");

                var result = testable.ClassUnderTest.Process("http://host/js1.js::http://host/js2.js");

                Assert.True(result.StartsWith("spritedir/"));
            }

            [Fact]
            public void WillReturnProcessedJsUrlWithKeyInPath()
            {
                var testable = new TestableJavaScriptReducer();
                testable.Mock<IRRConfiguration>().Setup(x => x.SpriteVirtualPath).Returns("spritedir");
                var guid = Guid.NewGuid();
                var builder = new UriBuilder(testable.Mock<IRRConfiguration>().Object);

                var result = testable.ClassUnderTest.Process(guid, "http://host/js1.js::http://host/js2.js");

                Assert.Equal(guid, builder.ParseKey(result));
            }

            [Fact]
            public void WillUseHashOfUrlsIfNoKeyIsGiven()
            {
                var testable = new TestableJavaScriptReducer();
                testable.Mock<IRRConfiguration>().Setup(x => x.SpriteVirtualPath).Returns("spritedir");
                var guid = Hasher.Hash("http://host/js1.js::http://host/js2.js");
                var builder = new UriBuilder(testable.Mock<IRRConfiguration>().Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js::http://host/js2.js");

                Assert.Equal(guid, builder.ParseKey(result));
            }

            [Fact]
            public void WillReturnProcessedJsUrlWithARequestReducedFileName()
            {
                var testable = new TestableJavaScriptReducer();

                var result = testable.ClassUnderTest.Process("http://host/js1.js::http://host/js2.js");

                Assert.True(result.EndsWith("-" + new JavaScriptResource().FileName));
            }

            [Fact]
            public void WillDownloadContentOfEachOriginalJS()
            {
                var testable = new TestableJavaScriptReducer();

                var result = testable.ClassUnderTest.Process("http://host/js1.js::http://host/js2.js");

                testable.Mock<IWebClientWrapper>().Verify(x => x.Download<JavaScriptResource>("http://host/js1.js"), Times.Once());
                testable.Mock<IWebClientWrapper>().Verify(x => x.Download<JavaScriptResource>("http://host/js2.js"), Times.Once());
            }

            [Fact]
            public void WillSaveMinifiedAggregatedJS()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection());
                mockWebResponse.Setup(x => x.GetResponseStream()).Returns(new MemoryStream(new UTF8Encoding().GetBytes("js1")));
                var mockWebResponse2 = new Mock<WebResponse>();
                mockWebResponse2.Setup(x => x.Headers).Returns(new WebHeaderCollection());
                mockWebResponse2.Setup(x => x.GetResponseStream()).Returns(new MemoryStream(new UTF8Encoding().GetBytes("js2")));
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js")).Returns(mockWebResponse.Object);
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js2.js")).Returns(mockWebResponse2.Object);
                testable.Mock<IMinifier>().Setup(x => x.Minify<JavaScriptResource>("js1\rnjs2\r\n")).Returns("min");

                var result = testable.ClassUnderTest.Process("http://host/js1.js::http://host/js2.js");

                testable.Mock<IStore>().Verify(
                    x =>
                    x.Save(Encoding.UTF8.GetBytes("min").MatchEnumerable(), result,
                           "http://host/js1.js::http://host/js2.js"), Times.Once());
            }

            [Fact]
            public void WillAddASemiColonToLoadedJsIfItEdsInAClosingParen()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection());
                mockWebResponse.Setup(x => x.GetResponseStream()).Returns(new MemoryStream(new UTF8Encoding().GetBytes("js1()")));
                var mockWebResponse2 = new Mock<WebResponse>();
                mockWebResponse2.Setup(x => x.Headers).Returns(new WebHeaderCollection());
                mockWebResponse2.Setup(x => x.GetResponseStream()).Returns(new MemoryStream(new UTF8Encoding().GetBytes("js2")));
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js")).Returns(mockWebResponse.Object);
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js2.js")).Returns(mockWebResponse2.Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js::http://host/js2.js");

                testable.Mock<IMinifier>().Verify(x => x.Minify<JavaScriptResource>("js1();\r\njs2\r\n"), Times.Once());
            }

            [Fact]
            public void WillAddASemiColonToLoadedJsIfItEdsInAClosingBrace()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection());
                mockWebResponse.Setup(x => x.GetResponseStream()).Returns(new MemoryStream(new UTF8Encoding().GetBytes("{js1()}")));
                var mockWebResponse2 = new Mock<WebResponse>();
                mockWebResponse2.Setup(x => x.Headers).Returns(new WebHeaderCollection());
                mockWebResponse2.Setup(x => x.GetResponseStream()).Returns(new MemoryStream(new UTF8Encoding().GetBytes("js2")));
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js")).Returns(mockWebResponse.Object);
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js2.js")).Returns(mockWebResponse2.Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js::http://host/js2.js");

                testable.Mock<IMinifier>().Verify(x => x.Minify<JavaScriptResource>("{js1()};\r\njs2\r\n"), Times.Once());
            }

            [Fact]
            public void WillAddUrlToIgnoreListIfExpiresIsAtLeastAWeek()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection() { { "Expires", "Tue, 04 Oct 2011 06:09:12 GMT" } });
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js?qs=875")).Returns(mockWebResponse.Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js?qs=875::");

                testable.Mock<IRRConfiguration>().VerifySet(x => x.JavaScriptUrlsToIgnore = ",host/js1.js", Times.Once());
            }

            [Fact]
            public void WillAddUrlToIgnoreListIfExpiresIsAtLeastAWeekAndUrlHasNoQueryString()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection() { { "Expires", "Tue, 04 Oct 2011 06:09:12 GMT" } });
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js")).Returns(mockWebResponse.Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js::");

                testable.Mock<IRRConfiguration>().VerifySet(x => x.JavaScriptUrlsToIgnore = ",host/js1.js", Times.Once());
            }

            [Fact]
            public void WillAddUrlToIgnoreListIfExpiresIsAtLeastAWeekAppendingToExistingList()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                testable.Mock<IRRConfiguration>().Setup(x => x.JavaScriptUrlsToIgnore).Returns("url1");
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection() { { "Expires", "Tue, 04 Oct 2011 06:09:12 GMT" } });
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js?qs=875")).Returns(mockWebResponse.Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js?qs=875::");

                testable.Mock<IRRConfiguration>().VerifySet(x => x.JavaScriptUrlsToIgnore = "url1,host/js1.js", Times.Once());
            }

            [Fact]
            public void WillNotAddUrlToIgnoreListIfItAlreadyExists()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                testable.Mock<IRRConfiguration>().Setup(x => x.JavaScriptUrlsToIgnore).Returns("host/js1.js");
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection() { { "Expires", "Tue, 04 Oct 2011 06:09:12 GMT" } });
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js?qs=875")).Returns(mockWebResponse.Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js?qs=875::");

                testable.Mock<IRRConfiguration>().VerifySet(x => x.JavaScriptUrlsToIgnore = It.IsAny<string>(), Times.Never());
            }

            [Fact]
            public void WillSwallowFormatException()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                testable.Mock<IRRConfiguration>().Setup(x => x.JavaScriptUrlsToIgnore).Returns("host/js1.js");
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection() { { "Expires", "sdfsdfsdfsdf" } });
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js?qs=875")).Returns(mockWebResponse.Object);

                var ex = Record.Exception(() => testable.ClassUnderTest.Process("http://host/js1.js?qs=875::"));

                Assert.Null(ex);
            }

            [Theory]
            [InlineData("max-age=7200000, no-store")]
            [InlineData("max-age=7200000, no-cache")]
            [InlineData("max-age=7200, public")]
            public void WillAddUrlToIgnoreListIfMaxAgeIsAtLeastAWeekOrCachingIsTurnedOff(string cacheVal)
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection() { { "Cache-Control", cacheVal } });
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js?qs=875")).Returns(mockWebResponse.Object);

                var result = testable.ClassUnderTest.Process("http://host/js1.js?qs=875::");

                testable.Mock<IRRConfiguration>().VerifySet(x => x.JavaScriptUrlsToIgnore = ",host/js1.js", Times.Once());
            }

            [Fact]
            public void WillSwallowFormatExceptionFromParsingCachecontrol()
            {
                var testable = new TestableJavaScriptReducer();
                var mockWebResponse = new Mock<WebResponse>();
                testable.Mock<IRRConfiguration>().Setup(x => x.JavaScriptUrlsToIgnore).Returns("host/js1.js");
                mockWebResponse.Setup(x => x.Headers).Returns(new WebHeaderCollection() { { "Cache-Control", "max-age=notanum, public" } });
                testable.Mock<IWebClientWrapper>().Setup(x => x.Download<JavaScriptResource>("http://host/js1.js?qs=875")).Returns(mockWebResponse.Object);

                var ex = Record.Exception(() => testable.ClassUnderTest.Process("http://host/js1.js?qs=875::"));

                Assert.Null(ex);
            }
        }
    }
}