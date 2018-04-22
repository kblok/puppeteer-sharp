﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using System.Linq;

namespace PuppeteerSharp.Tests.Frame
{
    [Collection("PuppeteerLoaderFixture collection")]
    public class FrameManagementTests : PuppeteerPageBaseTest
    {
        [Fact]
        public async Task ShouldHandleNestedFrames()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/frames/nested-frames.html");
            Assert.Equal(TestConstants.NestedFramesDumpResult, FrameUtils.DumpFrames(Page.MainFrame));
        }

        [Fact]
        public async Task ShouldSendEventsWhenFramesAreManipulatedDynamically()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            // validate frameattached events
            var attachedFrames = new List<PuppeteerSharp.Frame>();

            Page.FrameAttached += (sender, e) => attachedFrames.Add(e.Frame);

            await FrameUtils.AttachFrame(Page, "frame1", "./assets/frame.html");

            Assert.Single(attachedFrames);
            Assert.Equal("/assets/frame.html", attachedFrames[0].Url);

            // validate framenavigated events
            var navigatedFrames = new List<PuppeteerSharp.Frame>();
            Page.FrameNavigated += (sender, e) => navigatedFrames.Add(e.Frame);

            await FrameUtils.NavigateFrame(Page, "frame1", "./empty.html");
            Assert.Single(navigatedFrames);
            Assert.Equal("/assets/frame.html", navigatedFrames[0].Url);

            // validate framedetached events
            var detachedFrames = new List<PuppeteerSharp.Frame>();
            Page.FrameDetached += (sender, e) => detachedFrames.Add(e.Frame);

            await FrameUtils.DetachFrame(Page, "frame1");
            Assert.Single(navigatedFrames);
            Assert.True(navigatedFrames[0].Detached);
        }

        [Fact]
        public async Task ShouldPersistMainFrameOnCrossProcessNavigation()
        {
            await Page.GoToAsync(TestConstants.EmptyPage);
            var mainFrame = Page.MainFrame;
            await Page.GoToAsync(TestConstants.CrossProcessUrl + "/empty.html");
            Assert.Equal(mainFrame, Page.MainFrame);
        }

        [Fact]
        public async Task ShouldNotSendAttachDetachEventsForMainFrame()
        {
            var hasEvents = false;
            Page.FrameAttached += (sender, e) => hasEvents = true;
            Page.FrameDetached += (sender, e) => hasEvents = true;

            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.False(hasEvents);
        }

        [Fact]
        public async Task ShouldDetachChildFramesOnNavigation()
        {
            var attachedFrames = new List<PuppeteerSharp.Frame>();
            var detachedFrames = new List<PuppeteerSharp.Frame>();
            var navigatedFrames = new List<PuppeteerSharp.Frame>();

            Page.FrameAttached += (sender, e) => attachedFrames.Add(e.Frame);
            Page.FrameDetached += (sender, e) => detachedFrames.Add(e.Frame);
            Page.FrameNavigated += (sender, e) => navigatedFrames.Add(e.Frame);

            await Page.GoToAsync(TestConstants.ServerUrl + "/frames/nested-frames.html");
            Assert.Equal(4, attachedFrames.Count);
            Assert.Empty(detachedFrames);
            Assert.Equal(5, navigatedFrames.Count);

            attachedFrames.Clear();
            detachedFrames.Clear();
            navigatedFrames.Clear();

            await Page.GoToAsync(TestConstants.EmptyPage);
            Assert.Empty(attachedFrames);
            Assert.Equal(4, detachedFrames.Count);
            Assert.Single(navigatedFrames);
        }

        [Fact]
        public async Task ShouldReportFrameName()
        {
            await FrameUtils.AttachFrame(Page, "theFrameId", TestConstants.EmptyPage);
            await Page.EvaluateFunctionAsync(@"url =>
            {
                const frame = document.createElement('iframe');
                frame.name = 'theFrameName';
                frame.src = url;
                document.body.appendChild(frame);
                return new Promise(x => frame.onload = x);
            }", TestConstants.EmptyPage);

            Assert.Equal(string.Empty, Page.Frames.ElementAt(0).Name);
            Assert.Equal("theFrameId", Page.Frames.ElementAt(1).Name);
            Assert.Equal("theFrameName", Page.Frames.ElementAt(2).Name);
        }

        [Fact]
        public async Task ShouldReportFrameParent()
        {
            await FrameUtils.AttachFrame(Page, "frame1", TestConstants.EmptyPage);
            await FrameUtils.AttachFrame(Page, "frame2", TestConstants.EmptyPage);

            Assert.Null(Page.Frames.ElementAt(0).ParentFrame);
            Assert.Equal(Page.MainFrame, Page.Frames.ElementAt(1).ParentFrame);
            Assert.Equal(Page.MainFrame, Page.Frames.ElementAt(2).ParentFrame);
        }
    }
}
