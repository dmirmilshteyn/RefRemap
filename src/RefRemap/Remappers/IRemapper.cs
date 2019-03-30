using System;
using System.Collections.Generic;
using System.Text;

namespace RefRemap.Remappers
{
    public interface IRemapper
    {
        void Remap();
        bool IsCompatible();
    }
}
