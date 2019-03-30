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

            var resolveOption = app.Option("-r|--resolve", "Attempt to resolve types after remapping.", CommandOptionType.NoValue); 

            app.OnExecute(async () => {
                var logger = new ConsoleLogger();

                if (string.IsNullOrEmpty(assemblyArgument.Value)) {
                    await logger.Log(LogLevel.Error, "No assembly specified.");
                    return 1;
                }
                if (!sourceOption.HasValue()) {
                    await logger.Log(LogLevel.Error, "No source assembly names specified.");
                    return 1;
                }
                if (!targetOption.HasValue()) {
                    await logger.Log(LogLevel.Error, "No target assembly specified.");
                    return 1;
                }
                if (!outputOption.HasValue()) {
                    await logger.Log(LogLevel.Error, "No output path specified.");
                    return 1;
                }

                var assemblyPath = Path.GetFullPath(assemblyArgument.Value);
                if (!File.Exists(assemblyPath)) {
                    await logger.Log(LogLevel.Error, "Input assembly not found.");
                    return 1;
                }

                var targetAssemblyPath = Path.GetFullPath(targetOption.Value());
                if (!File.Exists(targetAssemblyPath)) {
                    await logger.Log(LogLevel.Error, "Target assembly not found.");
                    return 1;
                }

                var outputPath = Path.GetFullPath(outputOption.Value());
                if (!Directory.Exists(Path.GetDirectoryName(outputPath))) {
                    Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                }

                var remapOptions = new RemapOptions();
                if (resolveOption.HasValue()) {
                    remapOptions.Resolve = true;
                }

                var remapper = new Remapper(logger, assemblyPath, sourceOption.Values);
                return await remapper.Remap(targetAssemblyPath, outputPath, remapOptions);
            });

            return app.Execute(args);
        }
    }
}
