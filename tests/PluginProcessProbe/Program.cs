using System.Diagnostics;
using System.Text;

string command = args.Length == 0 ? "echo" : args[0];

switch (command)
{
    case "echo":
        {
            string input = await Console.In.ReadToEndAsync();
            Console.Error.Write("stderr-log");
            Console.Out.Write(input);
            return 0;
        }

    case "exit":
        {
            Console.Out.Write("stdout-before-exit");
            Console.Error.Write("stderr-before-exit");
            return args.Length > 1 && int.TryParse(args[1], out int exitCode) ? exitCode : 1;
        }

    case "stdout-bytes":
        {
            int count = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
            Console.Out.Write(new string('o', count));
            return 0;
        }

    case "stderr-bytes":
        {
            int count = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
            Console.Error.Write(new string('e', count));
            return 0;
        }

    case "sleep":
        {
            int milliseconds = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
            await Task.Delay(milliseconds);
            return 0;
        }

    case "spawn-inherited-pipe":
        {
            int milliseconds = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
            string processPath = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to resolve the current process path.");
            var startInfo = new ProcessStartInfo
            {
                FileName = processPath,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
                CreateNoWindow = true
            };
            if (string.Equals(
                    Path.GetFileNameWithoutExtension(processPath),
                    "dotnet",
                    StringComparison.OrdinalIgnoreCase))
            {
                startInfo.ArgumentList.Add(typeof(Bukit.PluginProcessProbe.ProbeMarker).Assembly.Location);
            }
            startInfo.ArgumentList.Add("hold-inherited-pipe");
            startInfo.ArgumentList.Add(milliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture));

            using Process child = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start inherited-pipe child process.");
            await Task.Delay(milliseconds);
            return 0;
        }

    case "hold-inherited-pipe":
        {
            int milliseconds = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
            await Task.Delay(milliseconds);
            return 0;
        }

    case "ignore-stdin-then-mark":
        {
            int milliseconds = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
            await Task.Delay(milliseconds);
            await File.WriteAllTextAsync(args[2], "completed");
            return 0;
        }

    case "exit-without-reading-stdin":
        return args.Length > 1 && int.TryParse(args[1], out int noReadExitCode)
            ? noReadExitCode
            : 1;

    case "utf8":
        {
            await Console.OpenStandardOutput().WriteAsync(Encoding.UTF8.GetBytes("你好"));
            return 0;
        }

    case "env":
        {
            string name = args[1];
            Console.Out.Write(Environment.GetEnvironmentVariable(name) ?? "<missing>");
            return 0;
        }

    default:
        Console.Error.Write("unknown command");
        return 2;
}
