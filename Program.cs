#region header

// RunInGenie - Program.cs
// 
// Created by: Alistair J R Young (avatar) at 2021/01/17 1:07 PM.

#endregion

#region using

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

#endregion

namespace ArkaneSystems.RunInGenie
{
    internal static class Program
    {
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
            string InvokeWslpath (string path)
            {
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

        private static IConfiguration Configuration {

            get
            {
                IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("$.json", true, false)
                    .Build();
                return config;
            }
        }

        private static string Shell
        {
            get
            {
                string shell = Configuration["shell"];
                if(shell == null || shell.Equals(String.Empty)) {
                    shell = "sh";
                }
                return shell;
            }
        }

        private static string Distro
        {
            get
            {
                string distro = Configuration["distro"];
                return (distro != null) ? distro : String.Empty; 
            }
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
        }

        private static int Main (string[] args)
        {
            // First check if help has been requested; if so, print it.
            if (args.Length == 1 && (args[0] == "-h" || args[0] == "--help"))
            {
                Program.PrintHelp ();

                return 0;
            }

            try
            {
                // Set chosen distro and shell
                string distro = (Program.Distro == String.Empty) ? String.Empty : "-d {Program.Distro}";
                string arguments = $"{distro} genie -c {Program.Shell}";

                Process ps;
                if(args.Length > 0) {
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
