using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RefRemap
{
    class Remapper
    {
        private readonly ILogger logger;
        private readonly string assemblyPath;
        private readonly HashSet<string> sourceNames;

        public Remapper(ILogger logger, string assemblyPath, IEnumerable<string> sourceReferenceNames) {
            this.logger = logger;
            this.assemblyPath = assemblyPath;
            this.sourceNames = new HashSet<string>(sourceReferenceNames);
        }

        public async Task<int> Remap(string targetAssemblyPath, string outputPath, RemapOptions options) {
            await logger.Log(LogLevel.Info, $"Processing \'{assemblyPath}\'...");

            var targetName = Path.GetFileNameWithoutExtension(targetAssemblyPath);

            var contextSourceNames = new HashSet<string>(sourceNames);
            if (contextSourceNames.Contains(targetName)) {
                contextSourceNames.Remove(targetName);
            }

            var assemblyResolver = new AssemblyResolver();
            var moduleContext = new ModuleContext(assemblyResolver);

            assemblyResolver.DefaultModuleContext = moduleContext;
            assemblyResolver.EnableTypeDefCache = true;

            using (var module = ModuleDefMD.Load(assemblyPath)) {
                module.Context = moduleContext;
                assemblyResolver.AddToCache(module);

                using (var targetModule = ModuleDefMD.Load(targetAssemblyPath)) {
                    targetModule.Context = moduleContext;
                    assemblyResolver.AddToCache(targetModule);

                    var context = new RemapContext(module, targetModule, contextSourceNames, options);

                    context.Remap();

                    module.Write(outputPath);
                }
            }

            // NOTE: References are only updated after the new module has been written
            using (var module = ModuleDefMD.Load(outputPath)) {
                var references = module.GetAssemblyRefs();
                if (references.Where(x => contextSourceNames.Contains(x.Name)).Any()) {
                    await logger.Log(LogLevel.Error, "Remap completed with errors. Some portions were not remapped.");
                    return 1;
                }
            }

            await logger.Log(LogLevel.Info, $"Remap completed for \'{assemblyPath}\'.");

            return 0;
        }
    }
}
