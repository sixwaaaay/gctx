// See https://aka.ms/new-console-template for more information


namespace gctx;

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;

/*
 * we want to create a tool to manage git context
 * which provide some commands to manage the git config
 * 1. update
 *  this command try to update current git context with the saved context that mapped with given name
 *  the saved context is a file contains the git config.
 *  the file name is like "{name}.{sha1}", the sha1 is the hash of the file content
 *  the saved files are stored in the folder "~/.gctx"
 *  if the file not exists, only when create option is specified, the file will be created,
 *  otherwise, the command will fail
 *  if the file exists, try to calculate the hash of the file content, if the hash is different,
 *  update the current git context with the file content and update the file name with the new hash
 *  if the hash is the same, do nothing and exit
 *
 *  logging what happened to the console, so the user can know what happened
 *
 * 2. list
 *  list all the saved git context, separate the name and the hash when list them
 *
 * 3. use
 *  use the saved git context with the given name
 *  if the name is not specified, use the default git context
 *  if default git context not exists, fail
 *  if the name is specified, try to find the saved git context with the given name
 *  if not exists, fail
 *  if exists, replace the current gitconfig with the saved git context
 *
 */

public abstract class Program
{
    private static readonly string Folder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gctx");

    public static int Main(string[] args)
    {
        // create a command line interface to manage the git context

        var fileOption = new Option<FileInfo>(
            aliases: new[] { "--gitconfig", "-g" },
            description: "The file path to the gitconfig file",
            getDefaultValue: () =>
                new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".gitconfig"))
        );

        var rootCommand = new RootCommand("A CLI to manage git context");
        rootCommand.AddGlobalOption(fileOption);


        var nameOption = new Option<string>(aliases: new[] { "--name", "-n" },
            description: "The name of the git context", getDefaultValue: () => "default");
        var createOption = new Option<bool>(aliases: new[] { "-c", "--create" }, description: "create if not exist",
            getDefaultValue: () => false);
        var updateCommand =
            new Command(name: "update", description: "update a git context, or create it optionally");
        updateCommand.AddOption(nameOption);
        updateCommand.AddOption(createOption);

        updateCommand.SetHandler((name, file, create) =>
            {
                if (!file.Exists)
                {
                    Console.WriteLine($"file {file.FullName} not exists");
                    return;
                }

                if (!Directory.Exists(Folder))
                {
                    Directory.CreateDirectory(Folder);
                }

                var savedFile = GetSavedGitContext(name);

                if (savedFile == null)
                {
                    if (!create)
                    {
                        Console.WriteLine($"No git context with the name {name} found");
                        return;
                    }

                    var newFile = new FileInfo(Path.Combine(Folder, $"{name}.{ShaFile(file)}"));
                    file.CopyTo(newFile.FullName);
                    Console.WriteLine($"Created a new git context with the name {name}");

                    return;
                }

                if (savedFile.Name.Split('.')[1] == ShaFile(file))
                {
                    Console.WriteLine($"The git context with the name {name} is up to date");
                    return;
                }

                Console.WriteLine($"The git context with the name {name} is outdated, updating...");
                file.CopyTo(savedFile.FullName, true);
                Console.WriteLine($"Updated the git context with the name {name}");
                // remove the old file
                savedFile.Delete();
            },
            nameOption, fileOption, createOption);

        var listCommand = new Command("list", "list all the saved git context");
        listCommand.SetHandler(() =>
        {
            var files = Directory.GetFiles(Folder);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var name = fileInfo.Name.Split('.')[0];
                Console.WriteLine($"{name} - {fileInfo.FullName}");
            }
        });


        var useCommand = new Command("use", "use the saved git context with the given name");
        useCommand.AddOption(nameOption);
        useCommand.SetHandler((name, file) =>
        {
            if (!file.Exists)
            {
                Console.WriteLine($"git config file {file.FullName} not exists");
                return;
            }

            var savedFile = GetSavedGitContext(name) ??
                            throw new Exception($"No git context with the name {name} found");
            savedFile.CopyTo(file.FullName, true);
            Console.WriteLine($"switched to git context {name}");
        }, nameOption, fileOption);

        rootCommand.AddCommand(useCommand);
        rootCommand.AddCommand(updateCommand);
        rootCommand.AddCommand(listCommand);


        var parser = new CommandLineBuilder(rootCommand)
            .UseDefaults()
            .UseExceptionHandler((exception, _) =>
                Console.Error.WriteLine($"An error occurred while executing the command: {exception.Message}"))
            .Build();

        return parser.Invoke(args);
    }

    private static string ShaFile(FileInfo file)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        using var stream = file.OpenRead();

        var hash = md5.ComputeHash(stream);


        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    // try to get the saved git context with the given name
    private static FileInfo? GetSavedGitContext(string name)
    {
        var files = Directory.GetFiles(Folder, $"{name}.*");
        return files.Length switch
        {
            0 => null,
            > 1 => throw new Exception($"There are more than one git context with the name {name}"),
            _ => new FileInfo(files[0])
        };
    }
}