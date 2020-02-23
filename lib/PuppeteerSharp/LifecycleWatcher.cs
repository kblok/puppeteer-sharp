using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Diagnostics.Contracts;
using PuppeteerSharp.Helpers;
using System.Collections.Concurrent;

namespace PuppeteerSharp
{
    internal class LifecycleWatcher : IDisposable
    {
        private static readonly Dictionary<WaitUntilNavigation, string> _puppeteerToProtocolLifecycle =
            new Dictionary<WaitUntilNavigation, string>
            {
                [WaitUntilNavigation.Load] = "load",
                [WaitUntilNavigation.DOMContentLoaded] = "DOMContentLoaded",
                [WaitUntilNavigation.Networkidle0] = "networkIdle",
                [WaitUntilNavigation.Networkidle2] = "networkAlmostIdle"
            };

        private static readonly WaitUntilNavigation[] _defaultWaitUntil = { WaitUntilNavigation.Load };

        private readonly FrameManager _frameManager;
        private readonly Frame _frame;
        private readonly IEnumerable<string> _expectedLifecycle;
        private readonly int _timeout;
        private readonly string _initialLoaderId;
        private Request _navigationRequest;
        private bool _hasSameDocumentNavigation;
        private TaskCompletionSource<bool> _newDocumentNavigationTaskWrapper;
        private TaskCompletionSource<bool> _sameDocumentNavigationTaskWrapper;
        private TaskCompletionSource<bool> _lifecycleTaskWrapper;
        private TaskCompletionSource<bool> _terminationTaskWrapper;
        private Task _timeoutOrTerminationTask;

        public LifecycleWatcher(
            FrameManager frameManager,
            Frame frame,
            WaitUntilNavigation[] waitUntil,
            int timeout)
        {
            _expectedLifecycle = (waitUntil ?? _defaultWaitUntil).Select(w =>
            {
                var protocolEvent = _puppeteerToProtocolLifecycle.GetValueOrDefault(w);
                Contract.Assert(protocolEvent != null, $"Unknown value for options.waitUntil: {w}");
                return protocolEvent;
            });

            _frameManager = frameManager;
            _frame = frame;
            _initialLoaderId = frame.LoaderId;
            _timeout = timeout;
            _hasSameDocumentNavigation = false;

            _sameDocumentNavigationTaskWrapper = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _newDocumentNavigationTaskWrapper = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _lifecycleTaskWrapper = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _terminationTaskWrapper = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            frameManager.LifecycleEvent += FrameManager_LifecycleEvent;
            frameManager.FrameNavigatedWithinDocument += NavigatedWithinDocument;
            frameManager.FrameDetached += OnFrameDetached;
            frameManager.NetworkManager.Request += OnRequest;
            frameManager.Client.Disconnected += OnClientDisconnected;

            CheckLifecycleComplete();
        }

        #region Properties
        public Task<Task> NavigationTask { get; internal set; }
        public Task<bool> SameDocumentNavigationTask => _sameDocumentNavigationTaskWrapper.Task;
        public Task<bool> NewDocumentNavigationTask => _newDocumentNavigationTaskWrapper.Task;
        public Response NavigationResponse => _navigationRequest?.Response;
        public Task TimeoutOrTerminationTask => _timeoutOrTerminationTask
            ?? (_timeoutOrTerminationTask = _terminationTaskWrapper.Task.WithTimeout(_timeout));
        public Task LifecycleTask => _lifecycleTaskWrapper.Task;

        #endregion

        #region Private methods

        private void OnClientDisconnected(object sender, EventArgs e)
            => Terminate(new TargetClosedException("Navigation failed because browser has disconnected!", _frameManager.Client.CloseReason));

        void FrameManager_LifecycleEvent(object sender, FrameEventArgs e) => CheckLifecycleComplete();

        private void OnFrameDetached(object sender, FrameEventArgs e)
        {
            var frame = e.Frame;
            if (_frame == frame)
            {
                Terminate(new PuppeteerException("Navigating frame was detached"));
                return;
            }
            CheckLifecycleComplete();
        }

        private void CheckLifecycleComplete()
        {
            // We expect navigation to commit.
            if (!CheckLifecycle(_frame, _expectedLifecycle))
            {
                return;
            }
            _lifecycleTaskWrapper.TrySetResult(true);
            if (_frame.LoaderId == _initialLoaderId && !_hasSameDocumentNavigation)
            {
                return;
            }

            if (_hasSameDocumentNavigation)
            {
                _sameDocumentNavigationTaskWrapper.TrySetResult(true);
            }
            if (_frame.LoaderId != _initialLoaderId)
            {
                _newDocumentNavigationTaskWrapper.TrySetResult(true);
            }
        }

        private void Terminate(PuppeteerException ex) => _terminationTaskWrapper.TrySetException(ex);

        private void OnRequest(object sender, RequestEventArgs e)
        {
            if (e.Request.Frame != _frame || !e.Request.IsNavigationRequest)
            {
                return;
            }
            _navigationRequest = e.Request;
        }

        private void NavigatedWithinDocument(object sender, FrameEventArgs e)
        {
            if (e.Frame != _frame)
            {
                return;
            }
            _hasSameDocumentNavigation = true;
            CheckLifecycleComplete();
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

        public void Dispose() => Dispose(true);

        ~LifecycleWatcher() => Dispose(false);

        public void Dispose(bool disposing)
        {
            var exception = _terminationTaskWrapper.Task.Exception;
            exception = _timeoutOrTerminationTask.Exception;

            _frameManager.LifecycleEvent -= FrameManager_LifecycleEvent;
            _frameManager.FrameNavigatedWithinDocument -= NavigatedWithinDocument;
            _frameManager.FrameDetached -= OnFrameDetached;
            _frameManager.NetworkManager.Request -= OnRequest;
            _frameManager.Client.Disconnected -= OnClientDisconnected;
        }

        #endregion
    }
}