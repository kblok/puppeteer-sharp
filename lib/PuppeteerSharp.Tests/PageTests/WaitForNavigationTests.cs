﻿using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.PageTests
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class WaitForNavigationTests : PuppeteerPageBaseTest
    {
        public WaitForNavigationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ShouldWork()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var waitForNavigationResult = Page.WaitForNavigationAsync();
            await Task.WhenAll(
                waitForNavigationResult,
                Page.EvaluateFunctionAsync("url => window.location.href = url", TestConstants.ServerUrl + "/grid.html")
            );
            var response = await waitForNavigationResult;
            Assert.Equal(HttpStatusCode.OK, response.Status);
            Assert.Contains("grid.html", response.Url);
        }

        [Fact]
        public async Task ShouldWorkWithBothDomcontentloadedAndLoad()
        {
            var responseCompleted = new TaskCompletionSource<bool>();
            Server.SetRoute("/one-style.css", context =>
            {
                return responseCompleted.Task;
            });
            var navigationTask = Page.GoToAsync(TestConstants.ServerUrl + "/one-style.html");
            var domContentLoadedTask = Page.WaitForNavigationAsync(new NavigationOptions
            {
                WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }
            });

            var bothFired = false;
            var bothFiredTask = Page.WaitForNavigationAsync(new NavigationOptions
            {
                WaitUntil = new[]
                {
                    WaitUntilNavigation.Load,
                    WaitUntilNavigation.DOMContentLoaded
                }
            }).ContinueWith(_ => bothFired = true);

            await Server.WaitForRequest("/one-style.css");
            await domContentLoadedTask;
            Assert.False(bothFired);
            responseCompleted.SetResult(true);
            await bothFiredTask;
            await navigationTask;
        }

        [Fact]
        public async Task ShouldWorkWithClickingOnAnchorLinks()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            await Page.SetContentAsync("<a href='#foobar'>foobar</a>");
            await Task.WhenAll(
                Page.ClickAsync("a"),
                Page.WaitForNavigationAsync()
            );
            Assert.Equal(TestConstants.EmptyPage + "#foo", Page.Url);
        }

        [Fact]
        public async Task ShouldWorkWithHistoryPushState()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            await Page.SetContentAsync(@"
              <a onclick='javascript:pushState()'>SPA</a>
              <script>
                function pushState() { history.pushState({}, '', 'wow.html') }
              </script>
            ");
            await Task.WhenAll(
                Page.ClickAsync("a"),
                Page.WaitForNavigationAsync()
            );
            Assert.Equal(TestConstants.EmptyPage + "wow.html", Page.Url);
        }

        [Fact]
        public async Task ShouldWorkWithHistoryReplaceState()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            await Page.SetContentAsync(@"
              <a onclick='javascript:pushState()'>SPA</a>
              <script>
                function pushState() { history.pushState({}, '', 'wow.html') }
              </script>
            ");
            await Task.WhenAll(
                Page.ClickAsync("a"),
                Page.WaitForNavigationAsync()
            );
            Assert.Equal(TestConstants.EmptyPage + "replaced.html", Page.Url);
        }

        [Fact]
        public async Task ShouldWorkWithDOMHistoryBackAndHistoryForward()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            await Page.SetContentAsync(@"
              <a id=back onclick='javascript:goBack()'>back</a>
              <a id=forward onclick='javascript:goForward()'>forward</a>
              <script>
                function goBack() { history.back(); }
                function goForward() { history.forward(); }
                history.pushState({}, '', '/first.html');
                history.pushState({}, '', '/second.html');
              </script>
            ");
            Assert.Equal(TestConstants.EmptyPage + "second.html", Page.Url);
            await Task.WhenAll(
                Page.ClickAsync("a#back"),
                Page.WaitForNavigationAsync()
            );
            Assert.Equal(TestConstants.EmptyPage + "first.html", Page.Url);
            await Task.WhenAll(
                Page.ClickAsync("a#forward"),
                Page.WaitForNavigationAsync()
            );
            Assert.Equal(TestConstants.EmptyPage + "second.html", Page.Url);
        }

        [Fact]
        public async Task ShouldWorkWhenSubframeIssuesWindowStop()
        {
            Server.SetRoute("/frames/style.css", context => Task.Delay(-1));
            var navigationTask = Page.GoToAsync(TestConstants.ServerUrl + "/frames/one-frame.html");
            var frameTsc = new TaskCompletionSource<Frame>();
            Page.FrameAttached += (sender, e) => frameTsc.TrySetResult(e.Frame);
            var frame = await frameTsc.Task;
            var frameNavigatedTsc = new TaskCompletionSource<bool>();
            Page.FrameNavigated += (sender, e) =>
            {
                if(e.Frame  == frame)
                {
                    frameNavigatedTsc.TrySetResult(true);
                }
            };
            await frameNavigatedTsc.Task;
            _ =frame.EvaluateExpressionAsync("window.stop()");
            await navigationTask;
        }
    }
}
