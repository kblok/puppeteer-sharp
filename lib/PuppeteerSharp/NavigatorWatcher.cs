﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics.Contracts;
using PuppeteerSharp.Helpers;
using System.Threading;

namespace PuppeteerSharp
{
    internal class NavigatorWatcher
    {
        private static readonly Dictionary<WaitUntilNavigation, string> _puppeteerToProtocolLifecycle = new Dictionary<WaitUntilNavigation, string>()
        {
            [WaitUntilNavigation.Load] = "load",
            [WaitUntilNavigation.DOMContentLoaded] = "DOMContentLoaded",
            [WaitUntilNavigation.Networkidle0] = "networkIdle",
            [WaitUntilNavigation.Networkidle2] = "networkAlmostIdle"
        };

        private readonly FrameManager _frameManager;
        private readonly Frame _frame;
        private readonly NavigationOptions _options;
        private readonly IEnumerable<string> _expectedLifecycle;
        private readonly int _timeout;
        private readonly string _initialLoaderId;

        private bool _hasSameDocumentNavigation;

        public NavigatorWatcher(FrameManager frameManager, Frame mainFrame, int timeout, NavigationOptions options)
        {
            var waitUntil = new[] { WaitUntilNavigation.Load };

            if (options?.WaitUntil != null)
            {
                waitUntil = options.WaitUntil;
            }

            _expectedLifecycle = waitUntil.Select(w =>
            {
                var protocolEvent = _puppeteerToProtocolLifecycle.GetValueOrDefault(w);
                Contract.Assert(protocolEvent != null, $"Unknown value for options.waitUntil: {w}");
                return protocolEvent;
            });

            _frameManager = frameManager;
            _frame = mainFrame;
            _options = options;
            _initialLoaderId = mainFrame.LoaderId;
            _timeout = timeout;
            _hasSameDocumentNavigation = false;

            frameManager.LifecycleEvent += CheckLifecycleComplete;
            frameManager.FrameNavigatedWithinDocument += NavigatedWithinDocument;
            frameManager.FrameDetached += CheckLifecycleComplete;
            LifeCycleCompleteTaskWrapper = new TaskCompletionSource<bool>();

            NavigationTask = Task.WhenAny(new[]
            {
                CreateTimeoutTask(),
                LifeCycleCompleteTask,
            }).ContinueWith((task) =>
            {
                CleanUp();
                return task.GetAwaiter().GetResult();
            });
        }

        #region Properties
        public Task<Task> NavigationTask { get; internal set; }
        public Task<bool> LifeCycleCompleteTask => LifeCycleCompleteTaskWrapper.Task;
        public TaskCompletionSource<bool> LifeCycleCompleteTaskWrapper { get; }

        #endregion

        #region Public methods
        public void Cancel() => CleanUp();
        #endregion
        #region Private methods

        private void CheckLifecycleComplete(object sender, FrameEventArgs e)
        {
            // We expect navigation to commit.
            if (_frame.LoaderId == _initialLoaderId && !_hasSameDocumentNavigation)
            {
                return;
            }
            if (!CheckLifecycle(_frame, _expectedLifecycle))
            {
                return;
            }

            if (!LifeCycleCompleteTaskWrapper.Task.IsCompleted)
            {
                LifeCycleCompleteTaskWrapper.SetResult(true);
            }
        }

        private void NavigatedWithinDocument(object sender, FrameEventArgs e)
        {
            if (e.Frame != _frame)
            {
                return;
            }
            _hasSameDocumentNavigation = true;
            CheckLifecycleComplete(sender, e);
        }

        private bool CheckLifecycle(Frame frame, IEnumerable<string> expectedLifecycle)
        {
            foreach (var item in expectedLifecycle)
            {
                if (!frame.LifecycleEvents.Contains(item))
                {
                    return false;
                }
            }
            foreach (var child in frame.ChildFrames)
            {
                if (!CheckLifecycle(child, expectedLifecycle))
                {
                    return false;
                }
            }
            return true;
        }

        private void CleanUp()
        {
            _frameManager.LifecycleEvent -= CheckLifecycleComplete;
            _frameManager.FrameDetached -= CheckLifecycleComplete;
        }

        private async Task CreateTimeoutTask()
        {
            var wrapper = new TaskCompletionSource<bool>();

            if (_timeout == 0)
            {
                await Task.Delay(-1);
            }
            else
            {
                await Task.Delay(_timeout);
                throw new NavigationException($"Navigation Timeout Exceeded: {_timeout}ms exceeded");
            }
        }

        #endregion
    }
}