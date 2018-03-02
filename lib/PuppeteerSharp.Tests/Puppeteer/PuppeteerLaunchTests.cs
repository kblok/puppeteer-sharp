﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace PuppeteerSharp.Tests.Puppeteer
{
    public class PuppeteerLaunchTests
    {
        public PuppeteerLaunchTests()
        {
            Downloader.CreateDefault().DownloadRevisionAsync(TestConstants.ChromiumRevision).GetAwaiter().GetResult();
        }

        [Fact]
        public async Task ShouldSupportIgnoreHTTPSErrorsOption()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.IgnoreHTTPSErrors = true;

            var browser = await PuppeteerSharp.Puppeteer.LaunchAsync(options, TestConstants.ChromiumRevision);
            var page = await browser.NewPageAsync();

            var response = await page.GoToAsync(TestConstants.HttpsPrefix + "/empty.html");
            Assert.Equal(response.Status.ToString(), "OK");

            await browser.CloseAsync();
        }

        [Fact]
        public async Task ShouldWorkInRealLife()
        {
            var options = TestConstants.DefaultBrowserOptions();

            var browser = await PuppeteerSharp.Puppeteer.LaunchAsync(options, TestConstants.ChromiumRevision);
            var page = await browser.NewPageAsync();

            var response = await page.GoToAsync("https://www.google.com");
            Assert.Equal(response.Status.ToString(), "OK");

            await browser.CloseAsync();
        }

        [Fact]
        public async Task NetworkRedirectsShouldReportSecurityDetails()
        {
            var options = TestConstants.DefaultBrowserOptions();
            options.IgnoreHTTPSErrors = true;

            var browser = await PuppeteerSharp.Puppeteer.LaunchAsync(options, TestConstants.ChromiumRevision);
            var page = await browser.NewPageAsync();

            // TODO:
            // httpsServer.setRedirect('/plzredirect', '/empty.html');

            var responses = new List<Response>();
            page.ResponseCreated += (sender, e) => responses.Add(e.Response);

            await page.GoToAsync(TestConstants.HttpsPrefix + "/plzredirect");
            Assert.Equal(2, responses.Count);
            Assert.Equal(HttpStatusCode.Redirect, responses[0].Status);
            var securityDetails = responses[0].SecurityDetails;
            Assert.Equal("TLS 1.2", securityDetails.Protocol);

            await page.CloseAsync();
            await browser.CloseAsync();
        }
    }
}
