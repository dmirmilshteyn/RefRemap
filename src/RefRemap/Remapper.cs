using dnlib.DotNet;
using System;
using System.Collections.Generic;
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
            using (var module = ModuleDefMD.Load(assemblyPath)) {
                using (var targetModule = ModuleDefMD.Load(targetAssemblyPath)) {
                    var context = new RemapContext(module, targetModule, sourceNames);

                    context.Remap();

                    module.Write(outputPath);
                }
            }
        }
    }
}
