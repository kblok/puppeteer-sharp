﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PuppeteerSharp.Input
{
    public class Mouse
    {
        private Session _client;
        private Keyboard _keyboard;
        private decimal _x = 0;
        private decimal _y = 0;
        private string _button = "none";

        public Mouse(Session client, Keyboard keyboard)
        {
            _client = client;
            _keyboard = keyboard;
        }

        public async Task Move(decimal x, decimal y, MoveOptions options = null)
        {
            options = options ?? new MoveOptions();

            decimal fromX = _x;
            decimal fromY = _y;
            _x = x;
            _y = y;
            int steps = options.Steps != null ? (int)options.Steps : 1;

            for (var i = 1; i <= steps; i++)
            {
                await _client.SendAsync("Input.dispatchMouseEvent", new Dictionary<string, object>(){
                    {"type", "mouseMoved"},
                    {"button", _button},
                    {"x", fromX + (_x - fromX) * ((decimal)i / steps)},
                    {"y", fromY + (_y - fromY) * ((decimal)i / steps)},
                    {"modifiers", _keyboard.Modifiers}
                });
            }
        }

        public async Task Click(decimal x, decimal y, ClickOptions options = null)
        {
            options = options ?? new ClickOptions();

            await Move(x, y);
            await Down(options);

            if (options.Delay != null)
            {
                await Task.Delay((int)options.Delay);
            }
            await Up(options);
        }

        public async Task Down(ClickOptions options = null)
        {
            options = options ?? new ClickOptions();

            _button = options.Button;

            await _client.SendAsync("Input.dispatchMouseEvent", new Dictionary<string, object>(){
                {"type", "mousePressed"},
                {"button", _button},
                {"x", _x},
                {"y", _y},
                {"modifiers", _keyboard.Modifiers},
                {"clickCount", options.ClickCount }
            });
        }

        public async Task Up(ClickOptions options = null)
        {
            options = options ?? new ClickOptions();

            _button = "none";

            await _client.SendAsync("Input.dispatchMouseEvent", new Dictionary<string, object>(){
                {"type", "mouseReleased"},
                {"button", options.Button},
                {"x", _x},
                {"y", _y},
                {"modifiers", _keyboard.Modifiers},
                {"clickCount", options.ClickCount }
            });
        }
    }
}
