using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
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

        public void Remap(string targetAssemblyPath, string outputPath) {
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
        }
    }
}
