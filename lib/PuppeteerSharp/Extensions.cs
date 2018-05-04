﻿using System.Threading.Tasks;

namespace PuppeteerSharp
{
    public static class Extensions
    {
        /// <summary>
        /// Runs <paramref name="pageFunction"/> within the frame and passes it the outcome of <paramref name="elementHandleTask"/> as the first argument
        /// </summary>
        /// <typeparam name="T">The type of the response</typeparam>
        /// <param name="elementHandleTask">A task that returns an <see cref="ElementHandle"/> that will be used as the first argument in <paramref name="pageFunction"/></param>
        /// <param name="pageFunction">Function to be evaluated in browser context</param>
        /// <param name="args">Arguments to pass to <c>pageFunction</c></param>
        /// <returns>Task which resolves to the return value of <c>pageFunction</c></returns>
        public static async Task<T> EvaluateFunctionAsync<T>(this Task<ElementHandle> elementHandleTask, string pageFunction, params object[] args)
        {
            var elementHandle = await elementHandleTask;
            if (elementHandle == null)
            {
                throw new PuppeteerException($"Error: failed to find element matching selector");
            }

            var newArgs = new object[args.Length + 1];
            newArgs[0] = elementHandle;
            args.CopyTo(newArgs, 1);
            var result = await elementHandle.Page.EvaluateFunctionAsync<T>(pageFunction, newArgs);
            await elementHandle.Dispose();
            return result;
        }
    }
}
