using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using PuppeteerSharp.Helpers;
using PuppeteerSharp.Transport;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.PuppeteerTests
{
    [Collection(TestConstants.TestFixtureCollectionName)]
    public class PuppeteerLaunchTests : PuppeteerBaseTest
    {
        public PuppeteerLaunchTests(ITestOutputHelper output) : base(output) { }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkInRealLife()
        {
            var options = TestConstants.DefaultBrowserOptions();

            using (var browser = await Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory))
            using (var page = await browser.NewPageAsync())
            {
                var response = await page.GoToAsync("https://www.google.com");
                Assert.Equal(HttpStatusCode.OK, response.Status);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectAllPromisesWhenBrowserIsClosed()
        {
            using (var browser = await Puppeteer.LaunchAsync(
                TestConstants.DefaultBrowserOptions(),
                TestConstants.LoggerFactory))
            using (var page = await browser.NewPageAsync())
            {
                var neverResolves = page.EvaluateFunctionHandleAsync("() => new Promise(r => {})");
                await browser.CloseAsync();
                var exception = await Assert.ThrowsAsync<TargetClosedException>(() => neverResolves);
                Assert.Contains("Protocol error", exception.Message);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectIfExecutablePathIsInvalid()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.ExecutablePath = "random-invalid-path";

            var exception = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            {
                return Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory);
            });

            Assert.Contains("Failed to launch", exception.Message);
            Assert.Equal(options.ExecutablePath, exception.FileName);
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task UserDataDirOption()
        {
            using (var userDataDir = new TempDirectory())
            {
                var options = TestConstants.DefaultBrowserOptions();
                options.UserDataDir = userDataDir.Path;

                var launcher = new Launcher(TestConstants.LoggerFactory);
                using (var browser = await launcher.LaunchAsync(options))
                {
                    Assert.True(Directory.GetFiles(userDataDir.Path).Length > 0);
                    await browser.CloseAsync();
                    Assert.True(Directory.GetFiles(userDataDir.Path).Length > 0);
                }
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task UserDataDirArgument()
        {
            using (var userDataDir = new TempDirectory())
            {
                var launcher = new Launcher(TestConstants.LoggerFactory);
                var options = TestConstants.DefaultBrowserOptions();

                if (TestConstants.IsChrome)
                {
                    options.Args = options.Args.Concat(new[] { $"--user-data-dir=\"{userDataDir}\"" }).ToArray();
                }
                else
                {
                    options.Args = options.Args.Concat(new string[] { "-profile", userDataDir.ToString() }).ToArray();
                }

                using (var browser = await launcher.LaunchAsync(options))
                {
                    // Open a page to make sure its functional.
                    await browser.NewPageAsync();
                    Assert.True(Directory.GetFiles(userDataDir.Path).Length > 0);
                    await browser.CloseAsync();
                    Assert.True(Directory.GetFiles(userDataDir.Path).Length > 0);
                }
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task UserDataDirOptionShouldRestoreState()
        {
            using (var userDataDir = new TempDirectory())
            {
                var launcher = new Launcher(TestConstants.LoggerFactory);
                var options = TestConstants.DefaultBrowserOptions();

                if (TestConstants.IsChrome)
                {
                    options.Args = options.Args.Concat(new[] { $"--user-data-dir=\"{userDataDir}\"" }).ToArray();
                }
                else
                {
                    options.Args = options.Args.Concat(new string[] { "-profile", userDataDir.ToString() }).ToArray();
                }

                using (var browser = await launcher.LaunchAsync(options))
                {
                    var page = await browser.NewPageAsync();
                    await page.GoToAsync(TestConstants.EmptyPage);
                    await page.EvaluateExpressionAsync("localStorage.hey = 'hello'");
                }

                using (var browser2 = await Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory))
                {
                    var page2 = await browser2.NewPageAsync();
                    await page2.GoToAsync(TestConstants.EmptyPage);
                    Assert.Equal("hello", await page2.EvaluateExpressionAsync<string>("localStorage.hey"));
                }
            }
        }

        [Fact(Skip = "This mysteriously fails on Windows on AppVeyor.")]
        public async Task UserDataDirOptionShouldRestoreCookies()
        {
            using (var userDataDir = new TempDirectory())
            {
                var launcher = new Launcher(TestConstants.LoggerFactory);
                var options = TestConstants.DefaultBrowserOptions();
                options.Args = options.Args.Concat(new[] { $"--user-data-dir=\"{userDataDir}\"" }).ToArray();

                using (var browser = await launcher.LaunchAsync(options))
                {
                    var page = await browser.NewPageAsync();
                    await page.GoToAsync(TestConstants.EmptyPage);
                    await page.EvaluateExpressionAsync(
                        "document.cookie = 'doSomethingOnlyOnce=true; expires=Fri, 31 Dec 9999 23:59:59 GMT'");
                }
                await TestUtils.WaitForCookieInChromiumFileAsync(userDataDir.Path, "doSomethingOnlyOnce");

                using (var browser2 = await Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory))
                {
                    var page2 = await browser2.NewPageAsync();
                    await page2.GoToAsync(TestConstants.EmptyPage);
                    Assert.Equal("doSomethingOnlyOnce=true", await page2.EvaluateExpressionAsync<string>("document.cookie"));
                }
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public void ShouldReturnTheDefaultArguments()
        {
            Assert.Contains("--headless", Puppeteer.GetDefaultArgs(TestConstants.DefaultBrowserOptions()));
            Assert.DoesNotContain("--headless", Puppeteer.GetDefaultArgs(new LaunchOptions
            {
                Headless = false
            }));

            if (TestConstants.IsChrome)
            {
                Assert.Contains("--no-first-run", Puppeteer.GetDefaultArgs(TestConstants.DefaultBrowserOptions()));
                Assert.Contains("--user-data-dir=\"foo\"", Puppeteer.GetDefaultArgs(new LaunchOptions
                {
                    UserDataDir = "foo"
                }));
            }
            else
            {
                Assert.Contains("--no-remote", Puppeteer.GetDefaultArgs(TestConstants.DefaultBrowserOptions()));
                Assert.Contains("--foreground", Puppeteer.GetDefaultArgs(TestConstants.DefaultBrowserOptions()));
                Assert.Contains("--profile", Puppeteer.GetDefaultArgs(new LaunchOptions
                {
                    UserDataDir = "foo",
                    Product = TestConstants.IsChrome ? Product.Chrome : Product.Firefox,
                }));
                Assert.Contains("\"foo\"", Puppeteer.GetDefaultArgs(new LaunchOptions
                {
                    UserDataDir = "foo",
                    Product = TestConstants.IsChrome ? Product.Chrome : Product.Firefox,
                }));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ChromeShouldBeClosed(bool useDisposeAsync)
        {
            var options = TestConstants.DefaultBrowserOptions();
            var launcher = new Launcher(TestConstants.LoggerFactory);

            using (var browser = await launcher.LaunchAsync(options))
            using (var page = await browser.NewPageAsync())
            {
                var response = await page.GoToAsync(TestConstants.EmptyPage);
                Assert.Equal(HttpStatusCode.OK, response.Status);

                if (useDisposeAsync)
                {
                    // emulates what would happen in a C#8 await using block
                    await browser.DisposeAsync();
                }
                else
                {
                    await browser.CloseAsync();
                }

                Assert.True(launcher.Process.HasExited);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ChromeShouldBeClosedOnDispose()
        {
            var options = TestConstants.DefaultBrowserOptions();
            var launcher = new Launcher(TestConstants.LoggerFactory);

            using (var browser = await launcher.LaunchAsync(options))
            using (var page = await browser.NewPageAsync())
            {
                var response = await page.GoToAsync(TestConstants.EmptyPage);
                Assert.Equal(HttpStatusCode.OK, response.Status);
            }

            Assert.True(await launcher.Process.WaitForExitAsync(TimeSpan.FromSeconds(10)));
            Assert.True(launcher.Process.HasExited);
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotOpenTwoChromesUsingTheSameLauncher()
        {
            var launcher = new Launcher(TestConstants.LoggerFactory);
            using (var browser = await launcher.LaunchAsync(TestConstants.DefaultBrowserOptions()))
            {
                var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                {
                    return launcher.LaunchAsync(TestConstants.DefaultBrowserOptions());
                });
                Assert.Equal("Unable to create or connect to another process", exception.Message);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNoDefaultArguments()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.IgnoreDefaultArgs = true;
            using (var browser = await Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory))
            using (var page = await browser.NewPageAsync())
            {
                Assert.Equal(121, await page.EvaluateExpressionAsync<int>("11 * 11"));
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterOutIgnoredDefaultArguments()
        {
            var defaultArgs = Puppeteer.GetDefaultArgs(TestConstants.DefaultBrowserOptions());
            var options = TestConstants.DefaultBrowserOptions();
            options.IgnoredDefaultArgs = new[] { defaultArgs[0], defaultArgs[2] };
            using (var browser = await Puppeteer.LaunchAsync(options))
            {
                var spawnargs = browser.Process.StartInfo.Arguments;
                Assert.DoesNotContain(defaultArgs[0], spawnargs);
                Assert.Contains(defaultArgs[1], spawnargs);
                Assert.DoesNotContain(defaultArgs[2], spawnargs);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveDefaultUrlWhenLaunchingBrowser()
        {
            using (var browser = await Puppeteer.LaunchAsync(TestConstants.DefaultBrowserOptions(), TestConstants.LoggerFactory))
            {
                var pages = (await browser.PagesAsync()).Select(page => page.Url);
                Assert.Equal(new[] { TestConstants.AboutBlank }, pages);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveCustomUrlWhenLaunchingBrowser()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.Args = options.Args.Prepend(TestConstants.EmptyPage).ToArray();
            using (var browser = await Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory))
            {
                var pages = await browser.PagesAsync();
                Assert.Single(pages);
                if (pages[0].Url != TestConstants.EmptyPage)
                {
                    await pages[0].WaitForNavigationAsync();
                }
                Assert.Equal(TestConstants.EmptyPage, pages[0].Url);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldSetTheDefaultViewport()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.DefaultViewport = new ViewPortOptions
            {
                Width = 456,
                Height = 789
            };

            using (var browser = await Puppeteer.LaunchAsync(options))
            using (var page = await browser.NewPageAsync())
            {
                Assert.Equal(456, await page.EvaluateExpressionAsync<int>("window.innerWidth"));
                Assert.Equal(789, await page.EvaluateExpressionAsync<int>("window.innerHeight"));
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldDisableTheDefaultViewport()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.DefaultViewport = null;

            using (var browser = await Puppeteer.LaunchAsync(options))
            using (var page = await browser.NewPageAsync())
            {
                Assert.Null(page.Viewport);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeFullPageScreenshotsWhenDefaultViewportIsNull()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.DefaultViewport = null;

            using (var browser = await Puppeteer.LaunchAsync(options))
            using (var page = await browser.NewPageAsync())
            {
                await page.GoToAsync(TestConstants.ServerUrl + "/grid.html");
                Assert.NotEmpty(await page.ScreenshotDataAsync(new ScreenshotOptions { FullPage = true }));
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCustomWebSocket()
        {
            var options = TestConstants.DefaultBrowserOptions();
            var customSocketCreated = false;
            options.WebSocketFactory = (uri, socketOptions, cancellationToken) =>
            {
                customSocketCreated = true;
                return WebSocketTransport.DefaultWebSocketFactory(uri, socketOptions, cancellationToken);
            };

            using (await Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory))
            {
                Assert.True(customSocketCreated);
            }
        }

        [Fact(Timeout = TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportCustomTransport()
        {
            var customTransportCreated = false;
            var options = TestConstants.DefaultBrowserOptions();
            options.TransportFactory = (url, opt, cancellationToken) =>
            {
                customTransportCreated = true;
                return WebSocketTransport.DefaultTransportFactory(url, opt, cancellationToken);
            };

            using (await Puppeteer.LaunchAsync(options, TestConstants.LoggerFactory))
            {
                Assert.True(customTransportCreated);
            }
        }
    }
}
