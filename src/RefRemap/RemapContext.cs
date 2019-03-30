using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace RefRemap
{
    public class RemapContext
    {
        public ModuleDefMD Module { get; }
        public ModuleDefMD TargetModule { get; }
        public HashSet<string> SourceAssemblies { get; }
        public RemapOptions Options { get; }

        public RemapContext(ModuleDefMD module, ModuleDefMD targetModule, HashSet<string> sourceAssemblies, RemapOptions options) {
            this.Module = module;
            this.TargetModule = targetModule;
            this.SourceAssemblies = sourceAssemblies;
            this.Options = options;
        }
    }
}
