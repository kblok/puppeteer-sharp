﻿using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Threading.Tasks;

namespace PuppeteerSharp.Input
{
    public class Touchscreen
    {
        private readonly Session _client;
        private readonly Keyboard _keyboard;

        public Touchscreen(Session client, Keyboard keyboard)
        {
            _client = client;
            _keyboard = keyboard;
        }

        public async Task Up(decimal x, decimal y)
        {
            var touchPoints = new[]{
                new {x= Math.Round(x), y = Math.Round(y)}
            };

            await _client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>(){
                {"type", "tochStart"},
                {"touchPoints", touchPoints},
                {"modifiers", _keyboard.Modifiers},
            });

            await _client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>(){
                {"type", "touchEnd"},
                {"touchPoints", touchPoints},
                {"modifiers", _keyboard.Modifiers},
            });
        }

        public async Task TapAsync(decimal x, decimal y)
        {
            // Touches appear to be lost during the first frame after navigation.
            // This waits a frame before sending the tap.
            // @see https://crbug.com/613219
            await _client.SendAsync("Runtime.evaluate", new
            {
                expression = "new Promise(x => requestAnimationFrame(() => requestAnimationFrame(x)))",
                awaitPromise = true
            });

            var touchPoints = new[] { new { x = Math.Round(x), y = Math.Round(y) } };
            await _client.SendAsync("Input.dispatchTouchEvent", new
            {
                type = "touchStart",
                touchPoints,
                modifiers = _keyboard.Modifiers
            });
            await _client.SendAsync("Input.dispatchTouchEvent", new
            {
                type = "touchEnd",
                touchPoints = Array.Empty<object>(),
                modifiers = _keyboard.Modifiers
            });
        }
    }
}
