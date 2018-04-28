﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace PuppeteerSharp.Tests.Input
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class InputTests : PuppeteerPageBaseTest
    {
        [Fact]
        public async Task ShouldClickTheButton()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html");
            await Page.ClickAsync("button");
            Assert.Equal("Clicked", await Page.EvaluateExpressionAsync<string>("result"));
        }

        [Fact]
        public async Task ShouldClickOnCheckboxInputAndToggle()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/checkbox.html");
            Assert.Null(await Page.EvaluateExpressionAsync("result.check"));
            await Page.ClickAsync("input#agree");
            Assert.True(await Page.EvaluateExpressionAsync<bool>("result.check"));
            Assert.Equal(new[] {
               "mouseover",
               "mouseenter",
               "mousemove",
               "mousedown",
               "mouseup",
               "click",
               "change"
            }, await Page.EvaluateExpressionAsync<string[]>("result.events"));
            await Page.ClickAsync("input#agree");
            Assert.False(await Page.EvaluateExpressionAsync<bool>("result.check"));
        }

        [Fact]
        public async Task ShouldClickOnCheckboxLabelAndToggle()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/checkbox.html");
            Assert.Null(await Page.EvaluateExpressionAsync("result.check"));
            await Page.ClickAsync("label[for=\"agree\"]");
            Assert.Equal(new[] {
                "click",
                "change"
            }, await Page.EvaluateExpressionAsync<string[]>("result.events"));
            await Page.ClickAsync("label[for=\"agree\"]");
            Assert.False(await Page.EvaluateExpressionAsync<bool>("result.check"));
        }

        [Fact]
        public async Task ShouldFailToClickAMissingButton()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html");
            var exception = await Assert.ThrowsAsync<PuppeteerException>(()
                => Page.ClickAsync("button.does-not-exist"));
            Assert.Equal("No node found for selector: button.does-not-exist", exception.Message);
        }

        // https://github.com/GoogleChrome/puppeteer/issues/161
        [Fact]
        public async Task ShouldNotHangWithTouchEnabledViewports()
        {
            await Page.SetViewport(TestConstants.IPhone.ViewPort);
            await Page.Mouse.Down();
            await Page.Mouse.Move(100, 10);
            await Page.Mouse.Up();
        }

        [Fact]
        public async Task ShouldTypeIntoTheTextarea()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/textarea.html");

            var textarea = await Page.GetElementAsync("textarea");
            await textarea.TypeAsync("Type in this text!");
            Assert.Equal("Type in this text!", await Page.EvaluateExpressionAsync<string>("result"));
        }

        [Fact]
        public async Task ShouldClickTheButtonAfterNavigation()
        {

        }

        [Fact]
        public async Task ShouldUploadTheFile()
        {

        }

        [Fact]
        public async Task ShouldMoveWithTheArrowKeys()
        {

        }

        [Fact]
        public async Task ShouldSendACharacterWithElementHandlePress()
        {

        }

        [Fact]
        public async Task ShouldSendACharacterWithSendCharacter()
        {

        }

        [Fact]
        public async Task ShouldReportShiftKey()
        {

        }

        [Fact]
        public async Task ShouldReportMultipleModifiers()
        {

        }

        [Fact]
        public async Task ShouldSendProperCodesWhileTyping()
        {

        }

        [Fact]
        public async Task ShouldSendProperCodesWhileTypingWithShift()
        {

        }

        [Fact]
        public async Task ShouldNotTypeCanceledEvents()
        {

        }

        [Fact]
        public async Task KeyboardModifier()
        {

        }

        [Fact]
        public async Task ShouldResizeTheTextarea()
        {

        }

        [Fact]
        public async Task ShouldScrollAndClickTheButton()
        {

        }

        [Fact]
        public async Task ShouldDoubleClickTheButton()
        {

        }

        [Fact]
        public async Task ShouldClickAPartiallyObscuredButton()
        {

        }

        [Fact]
        public async Task ShouldSelectTheTextWithMouse()
        {

        }

        [Fact]
        public async Task ShouldSelectTheTextByTripleClicking()
        {

        }

        [Fact]
        public async Task ShouldTriggerHoverState()
        {

        }

        [Fact]
        public async Task ShouldFireContextmenuEventOnRightClick()
        {

        }

        [Fact]
        public async Task ShouldSetModifierKeysOnClick()
        {

        }

        [Fact]
        public async Task ShouldSpecifyRepeatProperty()
        {

        }

        [Fact]
        public async Task ShouldClickLinksWhichCauseNavigation()
        {

        }

        [Fact]
        public async Task ShouldTweenMouseMovement()
        {

        }

        [Fact]
        public async Task ShouldTapTheButton()
        {

        }

        [Fact]
        public async Task ShouldReportTouches()
        {

        }

        [Fact]
        public async Task ShouldClickTheButtonInsideAnIframe()
        {

        }

        [Fact]
        public async Task ShouldClickTheButtonWithDeviceScaleFactorSet()
        {

        }

        [Fact]
        public async Task ShouldTypeAllKindsOfCharacters()
        {

        }

        [Fact]
        public async Task ShouldSpecifyLocation()
        {

        }

        [Fact]
        public async Task ShouldThrowOnUnknownKeys()
        {

        }
    }
}