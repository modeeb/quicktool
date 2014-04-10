using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace QuickTool
{
    class NetSh
    {
        public static IEnumerable<dynamic> Profiles
        {
            get { return Show(); }
        }

        public static IEnumerable<dynamic> Show()
        {
            var output = RunExternalExe("netsh", "wlan show profiles");
            
            IEnumerable<string> lines = output.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            
            lines = lines.SkipWhile(line => !line.StartsWith("Group policy profiles (read only)"))
                         .Skip(2)
                         .SkipWhile(line => line.Contains("<None>"));
            var groupProfiles = ParseProfiles(lines.TakeWhile(line => !line.StartsWith("User profiles")));

            lines = lines.SkipWhile(line => !line.StartsWith("User profiles"))
                         .Skip(2)
                         .SkipWhile(line => line.Contains("<None>"));
            var userProfiles = ParseProfiles(lines);

            return groupProfiles.Union(userProfiles);
        }

        private static IEnumerable<dynamic> ParseProfiles(IEnumerable<string> list)
        {
            foreach (var attribs in list.Select(profile => profile.Split(':')))
            {
                yield return new
                {
                    Name = attribs[1].Trim(),
                    Type = attribs[0].Trim()
                };
            }
        }

        public static string ShellExecute(string command)
        {
            var process = new Process();
            var startInfo = new ProcessStartInfo
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                FileName = "cmd.exe",
                Arguments = "/C " + command,
                UseShellExecute = false,
                RedirectStandardOutput = true
            };
            process.StartInfo = startInfo;
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return output;
        }

        public static string RunExternalExe(string filename, string arguments = null)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = filename,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            if (!string.IsNullOrEmpty(arguments))
            {
                process.StartInfo.Arguments = arguments;
            }

            var stdOutput = new StringBuilder();
            process.OutputDataReceived += (sender, args) => stdOutput.AppendLine(args.Data);

            string stdError;
            try
            {
                process.Start();
                process.BeginOutputReadLine();
                stdError = process.StandardError.ReadToEnd();
                process.WaitForExit();
            }
            catch (Exception e)
            {
                throw new Exception("OS error while executing " + Format(filename, arguments) + ": " + e.Message, e);
            }

            if (process.ExitCode == 0)
            {
                return stdOutput.ToString();
            }
            
            var message = new StringBuilder();

            if (!string.IsNullOrEmpty(stdError))
            {
                message.AppendLine(stdError);
            }

            if (stdOutput.Length != 0)
            {
                message.AppendLine("Std output:");
                message.AppendLine(stdOutput.ToString());
            }

            throw new Exception(Format(filename, arguments) + " finished with exit code = " + process.ExitCode + ": " + message);
        }

        private static string Format(string filename, string arguments)
        {
            return "'" + filename +
                ((string.IsNullOrEmpty(arguments)) ? string.Empty : " " + arguments) +
                "'";
        }
    }
}
