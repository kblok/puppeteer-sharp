using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PuppeteerSharp.Messaging;

namespace PuppeteerSharp.Helpers
{
    /// <summary>
    /// Provides an async queue for responses for <see cref="CDPSession.SendAsync"/>, so that responses can be handled
    /// async without risk callers causing a deadlock.
    /// </summary>
    /// <remarks>
    /// See https://github.com/hardkoded/puppeteer-sharp/issues/1354
    /// </remarks>
    internal class SendAsyncResponseQueue : IDisposable
    {
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", Justification = "False positive, as it is disposed in Dispose() but after copying to local variable.")]
        private CancellationTokenSource _disposing;

        private readonly ILogger _logger;

        public SendAsyncResponseQueue(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
            _disposing = new CancellationTokenSource();

            // TODO: make this async behavior optional, to avoid introducing races into unknown message handling scenarios
        }

        public void Enqueue(MessageTask callback, ConnectionResponse obj)
        {
            if (_disposing.IsCancellationRequested)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            Task.Run(() => HandleAsyncMessage(callback, obj), _disposing.Token)
                .ContinueWith(t => _logger.LogError(t.Exception, "Failed to complete async handling of SendAsync for {callback}", callback.Method), TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Dispose()
        {
            var cts = Interlocked.CompareExchange(ref _disposing, null, _disposing);
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        private static void HandleAsyncMessage(MessageTask callback, ConnectionResponse obj)
        {
            if (obj.Error != null)
            {
                callback.TaskWrapper.TrySetException(new MessageException(callback, obj.Error));
            }
            else
            {
                callback.TaskWrapper.TrySetResult(obj.Result);
            }
        }
    }
}
