using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RefRemap
{
    class Remapper
    {
        private readonly string assemblyPath;
        private readonly HashSet<string> sourceNames;

        public Remapper(string assemblyPath, IEnumerable<string> sourceReferenceNames) {
            this.assemblyPath = assemblyPath;
            this.sourceNames = new HashSet<string>(sourceReferenceNames);
        }

        public int Remap(string targetAssemblyPath, string outputPath) {
            var targetName = Path.GetFileNameWithoutExtension(targetAssemblyPath);

            var contextSourceNames = new HashSet<string>(sourceNames);
            if (contextSourceNames.Contains(targetName)) {
                contextSourceNames.Remove(targetName);
            }

            using (var module = ModuleDefMD.Load(assemblyPath)) {
                using (var targetModule = ModuleDefMD.Load(targetAssemblyPath)) {
                    var context = new RemapContext(module, targetModule, contextSourceNames);

                    context.Remap();

                    module.Write(outputPath);
                }
            }

            // NOTE: References are only updated after the new module has been written
            using (var module = ModuleDefMD.Load(outputPath)) {
                var references = module.GetAssemblyRefs();
                if (references.Where(x => contextSourceNames.Contains(x.Name)).Any()) {
                    Console.Error.WriteLine("Remap completed with errors. Some portions were not remapped.");
                    return 1;
                }
            }

            return 0;
        }
    }
}
