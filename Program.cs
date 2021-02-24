#region header

// RunInGenie - Program.cs
// 
// Created by: Alistair J R Young (avatar) at 2021/02/24 1:21 AM.

#endregion

#region using

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using Microsoft.Extensions.Configuration;

#endregion

namespace ArkaneSystems.RunInGenie
{
    internal static class Program
    {
        private static IConfiguration Configuration
        {
            get
            {
                IConfiguration config = new ConfigurationBuilder ()
                                       .AddJsonFile (path: "$.json", optional: true, reloadOnChange: false)
                                       .Build ();

                return config;
            }
        }

        private static string Shell
        {
            get
            {
                string shell                                                   = Program.Configuration[key: "shell"];
                if (shell == null || shell.Equals (value: string.Empty)) shell = "sh";

                return shell;
            }
        }

        private static string Distro
        {
            get
            {
                string distro = Program.Configuration[key: "distro"];

                return distro ?? string.Empty;
            }
        }

        private static bool IsWindowsPath (string arg)
        {
            try
            {
                string? tp = Path.GetFullPath (path: arg);

                return File.Exists (path: tp) || Directory.Exists (path: tp);
            }
            catch
            {
                return false;
            }
        }

        private static string TranslatePath (string path)
        {
            // ReSharper disable once VariableHidesOuterVariable
            string InvokeWslpath (string path)
            {
                // ReSharper disable once UseObjectOrCollectionInitializer
                Process wp = new ();
                wp.StartInfo.UseShellExecute        = false;
                wp.StartInfo.RedirectStandardOutput = true;
                wp.StartInfo.FileName               = "wsl";
                wp.StartInfo.Arguments              = $"wslpath -u '{path}'";
                wp.Start ();

                string output = wp.StandardOutput.ReadLine ()!;
                wp.WaitForExit ();

                return output;
            }

            string fullPath = Path.GetFullPath (path: path)!;

            // Check for UNC paths.
            if (fullPath.StartsWith (value: "\\\\", comparisonType: StringComparison.InvariantCulture))
                throw new InvalidOperationException (message: "Cannot translate UNC paths for WSL... yet.");

            // Check for drive letter.
            if (fullPath[index: 1] == ':')
            {
                var drive = new DriveInfo (driveName: fullPath);

                // Check for local hard drive.
                if (drive.DriveType == DriveType.Fixed)

                    // We can invoke wslpath.
                    return $"'{InvokeWslpath (path: fullPath)}'";

                // Otherwise...
                throw new InvalidOperationException (message: "Cannot translate paths for non-fixed drives... yet.");
            }

            // WTF cases?
            throw new InvalidOperationException (message: "What the path is this?");
        }

        private static void PrintHelp ()
        {
            ConsoleColor oldColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine (value: "RunInGenie - Copyright 2021 Arkane Systems\n");
            Console.ForegroundColor = oldColor;

            Console.WriteLine (value: "Easily run commands inside WSL and the genie bottle, capturing the exit code.");
            Console.WriteLine (value: "Windows paths that exist are automagically mapped.\n");

            Console.WriteLine (value: "Usage: Prefix WSL commands with $ to execute. For example:");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine (value: "$ systemctl status\n");
            Console.ForegroundColor = oldColor;

            Console.WriteLine (value: "$ without a following command starts a (non-login) shell.\n");

            Console.WriteLine (value: "A WSL distribution other than the default can be supplied using the -d/--distro option:");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine (value: "$ -d alpine systemctl status\n");
            Console.ForegroundColor = oldColor;
        }

        private static int Main (string[] args)
        {
            // Set default distro and shell
            string distro = Program.Distro == string.Empty ? string.Empty : "-d {Program.Distro}";
            string shell  = Program.Shell  == string.Empty ? "sh" : Program.Shell;

            // Cheap and nasty manual command parsing.
            // First check if help has been requested; if so, print it and exit.
            if (args.Length == 1 && (args[0] == "-h" || args[0] == "--help"))
            {
                Program.PrintHelp ();

                return 0;
            }

            // Second, check if a distro was specified (must be first argument).
            if (args.Length >= 1 && (args[0] == "-d" || args[0] == "--distro"))
            {
                if (args.Length == 1)
                {
                    Console.WriteLine (value: "If specifying a distro, you must specify a distro.");

                    return 1;
                }

                distro = $"-d {args[1]}";

                args = args.Skip (count: 2).ToArray ();
            }

            try
            {
                string arguments = $"{distro} genie -c {shell}";

                Process ps;

                if (args.Length > 0)
                {
                    // Check through additional arguments, one by one.
                    var param = new List<string> ();

                    foreach (var arg in args)

                        // Identify those which are probably Windows paths.
                        // And perform path translation.
                        param.Add (item: Program.IsWindowsPath (arg: arg) ? Program.TranslatePath (path: arg) : arg);

                    // Execute in WSL.
                    arguments = $"{arguments} -c \"{string.Join (separator: ' ', values: param)}\"";
                }

                ps = Process.Start (fileName: "wsl", arguments: arguments);
                ps.WaitForExit ();

                return ps.ExitCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine (value: $"$: error executing command: {ex.Message}");

                return 127;
            }
        }
    }
}
