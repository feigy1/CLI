using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace pro2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var output = new Option<FileInfo>("--output", "File path and name for the bundled output file");
            var folder = new Option<DirectoryInfo>("--folder", "folder containing files to bundle");
            var languages = new Option<string>("--languages", "Comma separated list of programming languages (e.g. 'cs,js,py') or 'all' to include all files")
            { IsRequired = true };
            var includeSource = new Option<bool>( "--note", "Include the source file paths as comments in the bundled file")
            { IsRequired = false };
            var sort = new Option<string>( "--sort","Sort files by 'name' or 'extension' (default is by name)")
            { IsRequired = false };
            var removeEmptyLines = new Option<bool>("--remove-empty-lines", "Remove empty lines from the source code before bundling")
            { IsRequired = false };
            var author = new Option<string>( "--author","The author of the code to be included in the bundle as a comment at the top")
            { IsRequired = false };
            output.AddAlias("-o");
            folder.AddAlias("-f");
            languages.AddAlias("-l");
            includeSource.AddAlias("-n");
            sort.AddAlias("-s");
            removeEmptyLines.AddAlias("-r");
            author.AddAlias("-a");

            var languageOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "c#", ".cs" },
                { "javascript", ".js" },
                { "python", ".py" },
                { "html", ".html" },
                { "java", ".java" },
                { "sql", ".sql" },
                { "c", ".c" },
                { "jsx", ".jsx" },
                { "c++", ".cpp" }
            };

            var bundleCommand = new Command("bundle", "Bundle code files to a single file");
            bundleCommand.AddOption(output);
            bundleCommand.AddOption(folder);
            bundleCommand.AddOption(languages);
            bundleCommand.AddOption(includeSource);
            bundleCommand.AddOption(sort);
            bundleCommand.AddOption(removeEmptyLines);
            bundleCommand.AddOption(author);

            bundleCommand.SetHandler((output, folder, languages, includeSource, sort, removeEmptyLines, author) =>
            {
                try
                {
                    if (folder == null || !folder.Exists)
                    {
                        Console.WriteLine("Error: Invalid or missing directory.");
                        return;
                    }

                    if (!Path.IsPathRooted(output.FullName))
                    {
                        output = new FileInfo(Path.Combine(Directory.GetCurrentDirectory(), output.FullName));
                    }

                    var files = folder.GetFiles("*.*", SearchOption.AllDirectories);
                    if (files.Length == 0)
                    {
                        Console.WriteLine("Error: No files found in the directory.");
                        return;
                    }

                    string[] includedingLanguages;
                    if (languages.ToLower() == "all")
                    {
                        includedingLanguages = files.Select(f => f.Extension).Distinct().ToArray();
                    }
                    else
                    {
                        var selectedLanguages = languages.Split(',').Select(lang => lang.Trim().ToLower()).ToList();
                        includedingLanguages = selectedLanguages
                            .Where(lang => languageOptions.ContainsKey(lang))
                            .Select(lang => languageOptions[lang])
                            .ToArray();
                    }

                    IOrderedEnumerable<FileInfo> sortedFiles;
                    if (sort == null)
                    {
                        sort = "name";
                    }
                    if (sort.ToLower() == "extension")
                    {
                        sortedFiles = files.OrderBy(f => f.Extension);
                    }
                    else
                    {
                        sortedFiles = files.OrderBy(f => f.Name);
                    }

                    using (var newFile = new StreamWriter(output.FullName))
                    {
                        if (!string.IsNullOrEmpty(author))
                        {
                            newFile.WriteLine($"// Author: {author}");
                        }

                        foreach (var file in sortedFiles)
                        {
                            if (includedingLanguages.Contains(file.Extension.ToLower()))
                            {
                                if (includeSource)
                                {
                                    var path = Path.GetFullPath(file.LinkTarget);//ניתוב מלא
                                    //var path = Path.GetRelativePath(folder.FullName, file.FullName);//ניתוב יחסי
                                    newFile.WriteLine($"// Source: {path}");
                                }

                                var content = File.ReadAllText(file.FullName);

                                if (removeEmptyLines)
                                {
                                    content = RemoveEmptyLines(content);
                                }

                                newFile.WriteLine(content);
                            }
                        }
                    }

                    Console.WriteLine($"Bundling complete. Output saved to {output.FullName}");
                }
                catch (DirectoryNotFoundException)
                {
                    Console.WriteLine("Error: folder not found");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }, output, folder, languages, includeSource, sort, removeEmptyLines, author);

            var createRspCommand = new Command("create-rsp", "Create a response file for bundle command");
            createRspCommand.AddAlias("-rsp");

            createRspCommand.SetHandler(() =>
            {
                try
                {
                    Console.Write("Enter the output file path (e.g., output.bundle): ");
                    string outputFilePath = Console.ReadLine()?.Trim();

                    Console.Write("Enter the folder to bundle (e.g., ./src): ");
                    string directoryPath = Console.ReadLine()?.Trim();

                    Console.Write("Enter the programming languages (comma-separated, e.g., 'cs,js'): ");
                    string languages = Console.ReadLine()?.Trim();

                    Console.Write("Include source file paths as comments? (y/n): ");
                    string includeSourceInput = Console.ReadLine()?.Trim().ToLower();
                    bool includeSource = includeSourceInput == "y" || includeSourceInput == "yes";

                    Console.Write("Sort files by name or extension (default: name): ");
                    string sort = Console.ReadLine()?.Trim();
                    if (string.IsNullOrEmpty(sort)) sort = "name"; 

                    Console.Write("Remove empty lines from source code? (y/n): ");
                    string removeEmptyLinesInput = Console.ReadLine()?.Trim().ToLower();
                    bool removeEmptyLines = removeEmptyLinesInput == "y" || removeEmptyLinesInput == "yes";

                    Console.Write("Enter the author name (optional): ");
                    string author = Console.ReadLine()?.Trim();

                    string fullCommand = $"--output \"{outputFilePath}\" --folder \"{directoryPath}\" --languages \"{languages}\"" +
                                         $" --note {(includeSource ? "true" : "false")} --sort {sort} --remove-empty-lines {(removeEmptyLines ? "true" : "false")}" +
                                         $"{(string.IsNullOrEmpty(author) ? "" : $" --author \"{author}\"")}";

                    string responseFileName = "fileName.rsp";
                    File.WriteAllText(responseFileName, fullCommand);

                    Console.WriteLine($"Response file created: {responseFileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            });

            var rootCommand = new RootCommand("Root command for CLI")
            {
                bundleCommand,
                createRspCommand
            };

            rootCommand.InvokeAsync(args).Wait();
        }
        private static string RemoveEmptyLines(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var nonEmptyLines = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
            return string.Join(Environment.NewLine, nonEmptyLines);
        }
    }
}
