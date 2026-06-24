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
