﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace PuppeteerSharp
{
    public class ExecutionContext
    {
        private readonly Session _client;
        private readonly int _contextId;

        public ExecutionContext(Session client, ContextPayload contextPayload, Func<dynamic, JSHandle> objectHandleFactory)
        {
            _client = client;
            _contextId = contextPayload.Id;
            FrameId = contextPayload.AuxData.FrameId;
            IsDefault = contextPayload.AuxData.IsDefault;
            ObjectHandleFactory = objectHandleFactory;
        }

        public Func<dynamic, JSHandle> ObjectHandleFactory { get; internal set; }
        public string FrameId { get; internal set; }
        public bool IsDefault { get; internal set; }

        public Task<object> EvaluateExpressionAsync(string script)
            => EvaluateExpressionAsync<object>(script);

        public Task<T> EvaluateExpressionAsync<T>(string script)
            => EvaluateAsync<T>(EvaluateExpressionHandleAsync(script));

        public Task<object> EvaluateFunctionAsync(string script, params object[] args)
            => EvaluateFunctionAsync<object>(script, args);

        public Task<T> EvaluateFunctionAsync<T>(string script, params object[] args)
            => EvaluateAsync<T>(EvaluateFunctionHandleAsync(script, args));

        internal async Task<JSHandle> EvaluateExpressionHandleAsync(string script)
        {
            if (string.IsNullOrEmpty(script))
            {
                return null;
            }

            return await EvaluateHandleAsync("Runtime.evaluate", new Dictionary<string, object>()
            {
                {"contextId", _contextId},
                {"expression", script},
                {"returnByValue", false},
                {"awaitPromise", true}
            });
        }

        internal async Task<JSHandle> EvaluateFunctionHandleAsync(string script, params object[] args)
        {
            if (string.IsNullOrEmpty(script))
            {
                return null;
            }

            return await EvaluateHandleAsync("Runtime.callFunctionOn", new Dictionary<string, object>()
            {
                {"functionDeclaration", script },
                {"executionContextId", _contextId},
                {"arguments", args.Select(FormatArgument)},
                {"returnByValue", false},
                {"awaitPromise", true}
            });
        }

        private async Task<T> EvaluateAsync<T>(Task<JSHandle> handleEvaluator)
        {
            var handle = await handleEvaluator;
            var result = await handle.JsonValue<T>()
                .ContinueWith(jsonTask => jsonTask.Exception != null ? default(T) : jsonTask.Result);

            await handle.Dispose();
            return result;
        }

        private async Task<JSHandle> EvaluateHandleAsync(string method, dynamic args)
        {
            dynamic response = await _client.SendAsync(method, args);

            if (response.exceptionDetails != null)
            {
                throw new EvaluationFailedException("Evaluation failed: " +
                    Helper.GetExceptionMessage(response.exceptionDetails.ToObject<EvaluateExceptionDetails>()));
            }

            return ObjectHandleFactory(response.result);
        }

        private object FormatArgument(object arg)
        {
            switch (arg)
            {
                case double d:
                    // no such thing as -0 in C# :)
                    if (double.IsPositiveInfinity(d)) return new { unserializableValue = "Infinity" };
                    if (double.IsNegativeInfinity(d)) return new { unserializableValue = "-Infinity" };
                    if (double.IsNaN(d)) return new { unserializableValue = "NaN" };
                    break;
                case JSHandle objectHandle:
                    if (objectHandle.ExecutionContext != this)
                        throw new PuppeteerException("JSHandles can be evaluated only in the context they were created!");
                    if (objectHandle.Disposed)
                        throw new PuppeteerException("JSHandle is disposed!");
                    if (objectHandle.RemoteObject.unserializableValue != null)
                        return new { objectHandle.RemoteObject.unserializableValue };
                    if (objectHandle.RemoteObject.objectId == null)
                        return new { objectHandle.RemoteObject.value };
                    return new { objectHandle.RemoteObject.objectId };
            }
            return new { value = arg };
        }

        public async Task<dynamic> QueryObjects(JSHandle prototypeHandle)
        {
            if (prototypeHandle.Disposed)
            {
                throw new ArgumentException("prototypeHandle is disposed", nameof(prototypeHandle));
            }

            if (!((IDictionary<string, object>)prototypeHandle.RemoteObject).ContainsKey("objectId"))
            {
                throw new ArgumentException("Prototype JSHandle must not be referencing primitive value",
                                            nameof(prototypeHandle));
            }

            dynamic response = await _client.SendAsync("Runtime.queryObjects", new Dictionary<string, object>()
            {
                {"prototypeObjectId", prototypeHandle.RemoteObject.objectId}
            });

            return ObjectHandleFactory(response.objects);
        }
    }
}