﻿using Jint.DebuggerExample.Commands;
using Jint.DebuggerExample.Debug;
using Jint.DebuggerExample.UI;
using Jint.Runtime.Debugger;
using System;
using System.Threading;

namespace Jint.DebuggerExample
{
    class Program
    {
        private static CommandManager commandManager;
        private static Debugger debugger;
        private static ScriptLoader scriptLoader;

        private static Display display;
        private static ScriptDisplay scriptDisplay;
        private static Prompt prompt;
        private static ErrorDisplay errorDisplay;
        private static TextDisplay textDisplay;

        private static AreaToggler mainDisplayToggler;

        static void Main(string[] args)
        {
            commandManager = new CommandManager()
                .Add(new Command("run", "Run without stepping", Run))
                .Add(new Command("pause", "Pause execution", Pause))
                .Add(new Command("stop", "Cancel execution", Stop))
                .Add(new Command("out", "Step out", StepOut))
                .Add(new Command("over", "Step over", StepOver))
                .Add(new Command("into", "Step into", StepInto))
                .Add(new Command("bp", "Toggle breakpoint", ToggleBreakPoint))
                .Add(new Command("help", "Display list of commands", ShowHelp))
                .Add(new Command("list", "Display source view", ShowSource))
                .Add(new Command("exit", "Exit the debuger", Exit));

            SetupDebugger(args);

            SetupUI();

            prompt.Command += Prompt_Command;

            prompt.Start();
            display.Start();
        }

        private static void SetupUI()
        {
            display = new Display();
            display.Ready += Display_Ready;

            // Simple Dispatcher and SynchronizationContext do the same job as in a typical GUI:
            // - SynchronizationContext Keeps async code continuations running on the main (UI) thread
            // - Dispatcher allows other threads to invoke Actions on the main (UI) thread
            SynchronizationContext.SetSynchronizationContext(new ConsoleSynchronizationContext(display));
            Dispatcher.Init(display);

            mainDisplayToggler = new AreaToggler();

            textDisplay = new TextDisplay(display);
            scriptDisplay = new ScriptDisplay(display, debugger);

            mainDisplayToggler
                .Add(textDisplay)
                .Add(scriptDisplay);

            prompt = new Prompt(display);

            errorDisplay = new ErrorDisplay(display);

            display.Add(scriptDisplay);
            display.Add(textDisplay);
            display.Add(prompt);
            display.Add(errorDisplay);
        }

        private static void SetupDebugger(string[] paths)
        {
            debugger = new Debugger();

            debugger.Pause += Debugger_Pause;
            debugger.Continue += Debugger_Continue;

            scriptLoader = new ScriptLoader(debugger);

            foreach (var path in paths)
            {
                scriptLoader.Load(path);
            }
        }

        private static void Display_Ready()
        {
            debugger.Execute();
        }

        private static void Debugger_Pause(DebugInformation info)
        {
            // We're being called from the Jint thread. Execute on UI thread:
            Dispatcher.Invoke(() => UpdateDisplay(info));
        }

        private static void Debugger_Continue()
        {
            // We're being called from the Jint thread. Execute on UI thread:
            Dispatcher.Invoke(() =>
            {
                scriptDisplay.ExecutingLocation = null;
            });
        }

        private static void UpdateDisplay(DebugInformation info)
        {
            string source = info.CurrentStatement.Location.Source;
            int startLine = info.CurrentStatement.Location.Start.Line;
            int endLine = info.CurrentStatement.Location.End.Line;

            scriptDisplay.Script = scriptLoader.GetScript(source);
            scriptDisplay.ExecutingLocation = info.CurrentStatement.Location;

            mainDisplayToggler.Show(scriptDisplay);
        }

        private static void Prompt_Command(string commandLine)
        {
            try
            {
                bool handled = commandManager.Parse(commandLine);
                if (!handled)
                {
                    throw new CommandException($"Unknown command '{commandLine}'. Type 'help' for a list of commands.");
                }
                else
                {
                    errorDisplay.Error = null;
                }
            }
            catch (CommandException ex)
            {
                errorDisplay.Error = ex.Message;
            }
        }

        private static void Run(string[] arguments)
        {
            debugger.Run();
        }

        private static void Pause(string[] arguments)
        {
            debugger.Stop();
        }

        private static void Stop(string[] arguments)
        {
            debugger.Cancel();
        }

        private static void StepOver(string[] arguments)
        {
            debugger.StepOver();
        }

        private static void StepOut(string[] arguments)
        {
            debugger.StepOut();
        }

        private static void StepInto(string[] arguments)
        {
            debugger.StepInto();
        }

        private static void ToggleBreakPoint(string[] arguments)
        {
            string scriptId = scriptDisplay.Script.Id;
            int line;
            switch (arguments.Length)
            {
                case 1:
                    line = Int32.Parse(arguments[0]);
                    break;
                case 2:
                    scriptId = arguments[0];
                    line = Int32.Parse(arguments[1]);
                    break;
                default:
                    throw new CommandException("Usage: bp <script id> [line number]");
            }

            if (debugger.HasBreakPoint(scriptId, line))
            {
                debugger.RemoveBreakPoint(scriptId, line);
            }
            else
            {
                if (!debugger.TryAddBreakPoint(scriptId, line))
                {
                    throw new CommandException($"Failed adding breakpoint at {scriptId} line {line}");
                }
            }

            if (scriptDisplay.Script.Id == scriptId)
            {
                scriptDisplay.Invalidate();
            }
        }

        private static void ShowSource(string[] arguments)
        {
            mainDisplayToggler.Show(scriptDisplay);
        }

        private static void ShowHelp(string[] arguments)
        {
            var help = commandManager.BuildHelp();
            textDisplay.Content = help;
            mainDisplayToggler.Show(textDisplay);
        }

        private static void Exit(string[] arguments)
        {
            debugger.Cancel();
            prompt.Stop();
            display.Stop();
        }
    }
}
