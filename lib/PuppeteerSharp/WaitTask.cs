using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PuppeteerSharp
{
    internal class WaitTask
    {
        private readonly DOMWorld _world;
        private readonly string _predicateBody;
        private readonly WaitForFunctionPollingOption _polling;
        private readonly int? _pollingInterval;
        private readonly int _timeout;
        private readonly object[] _args;
        private readonly Task _timeoutTimer;

        private readonly CancellationTokenSource _cts;
        private readonly TaskCompletionSource<JSHandle> _taskCompletion;

        private int _runCount;
        private bool _terminated;

        private const string WaitForPredicatePageFunction = @"
async function waitForPredicatePageFunction(predicateBody, polling, timeout, ...args) {
  const predicate = new Function('...args', predicateBody);
  let timedOut = false;
  if (timeout)
    setTimeout(() => timedOut = true, timeout);
  if (polling === 'raf')
    return await pollRaf();
  if (polling === 'mutation')
    return await pollMutation();
  if (typeof polling === 'number')
    return await pollInterval(polling);

  /**
   * @return {!Promise<*>}
   */
  function pollMutation() {
    const success = predicate.apply(null, args);
    if (success)
      return Promise.resolve(success);

    let fulfill;
    const result = new Promise(x => fulfill = x);
    const observer = new MutationObserver(mutations => {
      if (timedOut) {
        observer.disconnect();
        fulfill();
      }
      const success = predicate.apply(null, args);
      if (success) {
        observer.disconnect();
        fulfill(success);
      }
    });
    observer.observe(document, {
      childList: true,
      subtree: true,
      attributes: true
    });
    return result;
  }

  /**
   * @return {!Promise<*>}
   */
  function pollRaf() {
    let fulfill;
    const result = new Promise(x => fulfill = x);
    onRaf();
    return result;

    function onRaf() {
      if (timedOut) {
        fulfill();
        return;
      }
      const success = predicate.apply(null, args);
      if (success)
        fulfill(success);
      else
        requestAnimationFrame(onRaf);
    }
  }

  /**
   * @param {number} pollInterval
   * @return {!Promise<*>}
   */
  function pollInterval(pollInterval) {
    let fulfill;
    const result = new Promise(x => fulfill = x);
    onTimeout();
    return result;

    function onTimeout() {
      if (timedOut) {
        fulfill();
        return;
      }
      const success = predicate.apply(null, args);
      if (success)
        fulfill(success);
      else
        setTimeout(onTimeout, pollInterval);
    }
  }
}";

        internal WaitTask(
            DOMWorld world,
            string predicateBody,
            bool isExpression,
            string title,
            WaitForFunctionPollingOption polling,
            int? pollingInterval,
            int timeout,
            object[] args = null)
        {
            if (string.IsNullOrEmpty(predicateBody))
            {
                throw new ArgumentNullException(nameof(predicateBody));
            }
            if (pollingInterval <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pollingInterval), "Cannot poll with non-positive interval");
            }

            _world = world;
            _predicateBody = isExpression ? $"return ({predicateBody})" : $"return ({predicateBody})(...args)";
            _polling = polling;
            _pollingInterval = pollingInterval;
            _timeout = timeout;
            _args = args ?? Array.Empty<object>();

            _world.WaitTasks.Add(this);

            _cts = new CancellationTokenSource();

            if (timeout > 0)
            {
                _timeoutTimer = System.Threading.Tasks.Task.Delay(timeout, _cts.Token).ContinueWith(_
                    => Terminate(new WaitTaskTimeoutException(timeout, title)));
            }

            _taskCompletion = new TaskCompletionSource<JSHandle>(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = Rerun();
        }

        internal Task<JSHandle> Task => _taskCompletion.Task;

        internal async Task Rerun()
        {
            var runCount = Interlocked.Increment(ref _runCount);
            JSHandle success = null;
            Exception exception = null;

            var context = await _world.GetExecutionContextAsync().ConfigureAwait(false);
            try
            {
                success = await context.EvaluateFunctionHandleAsync(WaitForPredicatePageFunction,
                    new object[] { _predicateBody, _pollingInterval ?? (object)_polling, _timeout }.Concat(_args).ToArray()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (_terminated || runCount != _runCount)
            {
                if (success != null)
                {
                    await success.DisposeAsync().ConfigureAwait(false);
                }

                return;
            }
            if (exception == null &&
                await _world.EvaluateFunctionAsync<bool>("s => !s", success)
                    .ContinueWith(task => task.IsFaulted || task.Result)
                    .ConfigureAwait(false))
            {
                if (success != null)
                {
                    await success.DisposeAsync().ConfigureAwait(false);
                }

                return;
            }

            if (exception?.Message.Contains("Execution context was destroyed") == true)
            {
                _ = Rerun();
                return;
            }

            if (exception?.Message.Contains("Cannot find context with specified id") == true)
            {
                return;
            }

            if (exception != null)
            {
                _taskCompletion.TrySetException(exception);
            }
            else
            {
                _taskCompletion.TrySetResult(success);
            }
            Cleanup();
        }

        internal void Terminate(Exception exception)
        {
            _terminated = true;
            _taskCompletion.TrySetException(exception);
            Cleanup();
        }

        private void Cleanup()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
            _cts?.Dispose();
            _world.WaitTasks.Remove(this);
        }
    }
}
