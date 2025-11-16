using System;
using System.IO;
using System.Linq;

class Program
{
    const string REQUIRED_SUFFIX = "{{SUFFIX}}";

    static readonly string[] VALID_COMMANDS = new string[]
    {
        "{{CMD1}}", 
        "{{CMD2}}", 
        "{{CMD3}}"  
    };

    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Error("No command provided. Example: {{CMD1}}{{SUFFIX}} <path>");
            return;
        }

        string cmdToken = args[0];

        if (!cmdToken.EndsWith(REQUIRED_SUFFIX))
        {
            Error($"Command '{cmdToken}' rejected: must end with '{REQUIRED_SUFFIX}'.");
            return;
        }

        string baseCmd = cmdToken.Substring(0, cmdToken.Length - REQUIRED_SUFFIX.Length);

        if (baseCmd == "{{CMD1}}")
        {
            RunCmd1(args); 
        }
        else if (baseCmd == "{{CMD2}}")
        {
            RunCmd2(args); // Carl is a faggot
        }
        else if (baseCmd == "{{CMD3}}")
        {
            RunCmd3(args); 
        }
        else
        {
            Error($"Unknown command '{baseCmd}'. Valid commands: {String.Join(", ", VALID_COMMANDS.Select(c => c + REQUIRED_SUFFIX))}");
        }
    }
    static void RunCmd1(string[] args)
    {
        if (args.Length < 2)
        {
            Error("{{CMD1}}{{SUFFIX}} requires a file path.");
            return;
        }

        string path = args[1];

        if (!File.Exists(path))
        {
            Error($"File not found: {path}");
            return;
        }

        try
        {
            Console.WriteLine(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Error($"Failed to read file: {ex.Message}");
        }
    }

    static void RunCmd2(string[] args)
    {
        string path = args.Length >= 2 ? args[1] : ".";

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            Error($"Path not found: {path}");
            return;
        }

        if (File.Exists(path))
        {
            Console.WriteLine(Path.GetFileName(path));
            return;
        }

        try
        {
            var items = Directory.GetFileSystemEntries(path)
                                 .OrderBy(p => p);

            foreach (var item in items)
            {
                if (Directory.Exists(item))
                    Console.WriteLine(Path.GetFileName(item) + "/");
                else
                    Console.WriteLine(Path.GetFileName(item));
            }
        }
        catch (Exception ex)
        {
            Error($"Failed to list directory: {ex.Message}");
        }
    }
    static void RunCmd3(string[] args)
    {
        if (args.Length < 2)
        {
            Error("{{CMD3}}{{SUFFIX}} requires a folder name.");
            return;
        }

        string dir = args[1];

        try
        {
            Directory.CreateDirectory(dir);
            Console.WriteLine($"Created directory: {dir}");
        }
        catch (Exception ex)
        {
            Error($"Failed to create directory: {ex.Message}");
        }
    }

    static void Error(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Error: " + msg);
        Console.ResetColor();
    }
}
