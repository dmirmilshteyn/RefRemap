using Microsoft.Extensions.CommandLineUtils;
using System;
using System.IO;

namespace RefRemap
{
    class Program
    {
        static int Main(string[] args) {
            var app = new CommandLineApplication();
            app.HelpOption("-?|--help");

            var assemblyArgument = app.Argument("assembly", "The path to the assembly to be edited.");
            var sourceOption = app.Option("-s|--source", "Source assembly name to be remapped.", CommandOptionType.MultipleValue);
            var targetOption = app.Option("-t|--target", "Target assembly path.", CommandOptionType.SingleValue);
            var outputOption = app.Option("-o|--output", "Output path where the generated assembly will be written to.", CommandOptionType.SingleValue);

            app.OnExecute(() => {
                if (string.IsNullOrEmpty(assemblyArgument.Value)) {
                    Console.Error.WriteLine("No assembly specified.");
                    return 1;
                }
                if (!sourceOption.HasValue()) {
                    Console.Error.WriteLine("No source assembly names specified.");
                    return 1;
                }
                if (!targetOption.HasValue()) {
                    Console.Error.WriteLine("No target assembly specified.");
                    return 1;
                }
                if (!outputOption.HasValue()) {
                    Console.Error.WriteLine("No output path specified.");
                    return 1;
                }

                var assemblyPath = Path.GetFullPath(assemblyArgument.Value);
                if (!File.Exists(assemblyPath)) {
                    Console.Error.WriteLine("Input assembly not found.");
                    return 1;
                }

                var targetAssemblyPath = Path.GetFullPath(targetOption.Value());
                if (!File.Exists(targetAssemblyPath)) {
                    Console.Error.WriteLine("Target assembly not found.");
                    return 1;
                }

                var outputPath = Path.GetFullPath(outputOption.Value());
                if (!Directory.Exists(Path.GetDirectoryName(outputPath))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                }

                var remapper = new Remapper(assemblyPath, sourceOption.Values);
                return remapper.Remap(targetAssemblyPath, outputPath);
            });

            return app.Execute(args);
        }
    }
}
