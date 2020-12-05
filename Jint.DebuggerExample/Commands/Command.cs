﻿using System;

namespace Jint.DebuggerExample.Commands
{
    public class Command
    {
        public string Name { get; }
        public string Description { get; }
        public Action<string[]> Handler { get; }

        public Command(string name, string description, Action<string[]> handler)
        {
            Name = name;
            Description = description;
            Handler = handler;
        }
    }
}