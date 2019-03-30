using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace RefRemap.Remappers
{
    public abstract class AbstractRemapper : IRemapper
    {
        private readonly RemapContext context;

        public ModuleDefMD Module => context.Module;
        public ModuleDefMD TargetModule => context.TargetModule;
        public HashSet<string> SourceAssemblies => context.SourceAssemblies;
        public RemapOptions Options => context.Options;

        public AbstractRemapper(RemapContext context) {
            this.context = context;
        }

        public abstract void Remap();
        public abstract bool IsCompatible();
    }
}
