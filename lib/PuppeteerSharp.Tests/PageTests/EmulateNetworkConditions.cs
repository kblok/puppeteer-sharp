﻿using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace PuppeteerSharp.Tests.PageTests
{
    [Collection(TestConstants.TestFixtureCollectionName)]
    public class EmulateNetworkConditionsTests : PuppeteerPageBaseTest
    {
        public EmulateNetworkConditionsTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ShouldChangeNavigatorConnectionEffectiveType()
        {
            var slow3G = Puppeteer.NetworkConditions[PredefinedNetworkConditionsName.Slow3G];
            var fast3G = Puppeteer.NetworkConditions[PredefinedNetworkConditionsName.Fast3G];

            Assert.Equal("4g", await Page.EvaluateExpressionAsync<string>("window.navigator.connection.effectiveType").ConfigureAwait(false));
            await Page.EmulateNetworkConditions(fast3G);
            Assert.Equal("3g", await Page.EvaluateExpressionAsync<string>("window.navigator.connection.effectiveType").ConfigureAwait(false));
            await Page.EmulateNetworkConditions(slow3G);
            Assert.Equal("2g", await Page.EvaluateExpressionAsync<string>("window.navigator.connection.effectiveType").ConfigureAwait(false));
            await Page.EmulateNetworkConditions(null);
        }
    }
}
