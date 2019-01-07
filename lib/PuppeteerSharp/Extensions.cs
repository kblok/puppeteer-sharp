﻿using System.Threading.Tasks;

namespace PuppeteerSharp
{
    /// <summary>
    /// <see cref="JSHandle"/> and <see cref="ElementHandle"/> Extensions.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome of <paramref name="elementHandleTask"/> as the first argument
        /// </summary>
        /// <param name="elementHandleTask">A task that returns an <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task</returns>
        /// <exception cref="SelectorException">If <paramref name="elementHandleTask"/> resolves to <c>null</c></exception>
        public static Task EvaluateFunctionAsync(this Task<ElementHandle> elementHandleTask, string pageFunction, params object[] args)
            => elementHandleTask.EvaluateFunctionAsync<object>(pageFunction, args);

        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome of <paramref name="elementHandleTask"/> as the first argument
        /// </summary>
        /// <typeparam name="T">The type of the response</typeparam>
        /// <param name="elementHandleTask">A task that returns an <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task which resolves to the return value of <c>pageFunction</c></returns>
        /// <exception cref="SelectorException">If <paramref name="elementHandleTask"/> resolves to <c>null</c></exception>
        public static async Task<T> EvaluateFunctionAsync<T>(this Task<ElementHandle> elementHandleTask, string pageFunction, params object[] args)
        {
            var elementHandle = await elementHandleTask.ConfigureAwait(false);
            if (elementHandle == null)
            {
                throw new SelectorException("Error: failed to find element matching selector");
            }

            return await elementHandle.EvaluateFunctionAsync<T>(pageFunction, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome the <paramref name="elementHandle"/> as the first argument
        /// </summary>
        /// <typeparam name="T">The type of the response</typeparam>
        /// <param name="elementHandle">An <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task which resolves to the return value of <c>pageFunction</c></returns>
        /// <exception cref="SelectorException">If <paramref name="elementHandle"/> is <c>null</c></exception>
        public static async Task<T> EvaluateFunctionAsync<T>(this ElementHandle elementHandle, string pageFunction, params object[] args)
        {
            if (elementHandle == null)
            {
                throw new SelectorException("Error: failed to find element matching selector");
            }

            var newArgs = new object[args.Length + 1];
            newArgs[0] = elementHandle;
            args.CopyTo(newArgs, 1);
            var result = await elementHandle.ExecutionContext.EvaluateFunctionAsync<T>(pageFunction, newArgs).ConfigureAwait(false);
            await elementHandle.DisposeAsync().ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome the <paramref name="elementHandle"/> as the first argument
        /// </summary>
        /// <typeparam name="T">The type of the response</typeparam>
        /// <param name="elementHandle">An <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="disposeHandle">If set to <c>false</c> the <paramref name="elementHandle"/> will not be disposed. Only opt out of disposal if the <paramref name="pageFunction"/> is a pure function with no side effects on the <paramref name="elementHandle"/>.</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task which resolves to the return value of <c>pageFunction</c></returns>
        /// <exception cref="SelectorException">If <paramref name="elementHandle"/> is <c>null</c></exception>
        public static async Task<T> EvaluateFunctionAsync<T>(this ElementHandle elementHandle, string pageFunction, bool disposeHandle, params object[] args)
        {
            if (elementHandle == null)
            {
                throw new SelectorException("Error: failed to find element matching selector");
            }

            var newArgs = new object[args.Length + 1];
            newArgs[0] = elementHandle;
            args.CopyTo(newArgs, 1);
            var result = await elementHandle.ExecutionContext.EvaluateFunctionAsync<T>(pageFunction, newArgs).ConfigureAwait(false);

            if (disposeHandle)
            {
                await elementHandle.DisposeAsync().ConfigureAwait(false);
            }

            return result;
        }

        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome of <paramref name="arrayHandleTask"/> as the first argument. Use only after <see cref="Page.QuerySelectorAllHandleAsync(string)"/>
        /// </summary>
        /// <param name="arrayHandleTask">A task that returns an <see cref="JSHandle"/> that represents an array of <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task</returns>
        public static Task EvaluateFunctionAsync(this Task<JSHandle> arrayHandleTask, string pageFunction, params object[] args)
            => arrayHandleTask.EvaluateFunctionAsync<object>(pageFunction, args);

        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome of <paramref name="arrayHandleTask"/> as the first argument. Use only after <see cref="Page.QuerySelectorAllHandleAsync(string)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arrayHandleTask">A task that returns an <see cref="JSHandle"/> that represents an array of <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task which resolves to the return value of <c>pageFunction</c></returns>
        public static async Task<T> EvaluateFunctionAsync<T>(this Task<JSHandle> arrayHandleTask, string pageFunction, params object[] args)
            => await (await arrayHandleTask.ConfigureAwait(false)).EvaluateFunctionAsync<T>(pageFunction, args).ConfigureAwait(false);

        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome of <paramref name="arrayHandle"/> as the first argument. Use only after <see cref="Page.QuerySelectorAllHandleAsync(string)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arrayHandle">An <see cref="JSHandle"/> that represents an array of <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task which resolves to the return value of <c>pageFunction</c></returns>
        public static async Task<T> EvaluateFunctionAsync<T>(this JSHandle arrayHandle, string pageFunction, params object[] args)
        {
            var response = await arrayHandle.JsonValueAsync<object[]>().ConfigureAwait(false);

            var newArgs = new object[args.Length + 1];
            newArgs[0] = arrayHandle;
            args.CopyTo(newArgs, 1);
            var result = await arrayHandle.ExecutionContext.EvaluateFunctionAsync<T>(pageFunction, newArgs).ConfigureAwait(false);
            await arrayHandle.DisposeAsync().ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome of <paramref name="arrayHandle"/> as the first argument. Use only after <see cref="Page.QuerySelectorAllHandleAsync(string)"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arrayHandle">An <see cref="JSHandle"/> that represents an array of <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="disposeHandle">If set to <c>false</c> the <paramref name="arrayHandle"/> will not be disposed. Only opt out of disposal if the <paramref name="pageFunction"/> is a pure function with no side effects on the <paramref name="arrayHandle"/>.</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task which resolves to the return value of <c>pageFunction</c></returns>
        public static async Task<T> EvaluateFunctionAsync<T>(this JSHandle arrayHandle, string pageFunction, bool disposeHandle, params object[] args)
        {
            var response = await arrayHandle.JsonValueAsync<object[]>().ConfigureAwait(false);

            var newArgs = new object[args.Length + 1];
            newArgs[0] = arrayHandle;
            args.CopyTo(newArgs, 1);
            var result = await arrayHandle.ExecutionContext.EvaluateFunctionAsync<T>(pageFunction, newArgs).ConfigureAwait(false);

            if (disposeHandle)
            {
                await arrayHandle.DisposeAsync().ConfigureAwait(false);
            }

            return result;
        }
    }
}
