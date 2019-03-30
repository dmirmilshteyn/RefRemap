// Portions taken from ConfuserEx
// https://github.com/yck1509/ConfuserEx

//ConfuserEx is licensed under MIT license.

//----------------

//Copyright (c) 2014 yck1509

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.Text;

namespace RefRemap.Remappers
{
    public class WPFRemapper : AbstractRemapper
    {
        public WPFRemapper(RemapContext context) : base(context) {
        }

        public override void Remap() {
        }

        public override bool IsCompatible() {
            foreach (var assembly in Module.GetAssemblyRefs()) {
                if (assembly.Name == "WindowsBase" || assembly.Name == "PresentationCore" || assembly.Name == "PresentationFramework" || assembly.Name == "System.Xaml") {
                    return true;
                }
            }

            return false;
        }
    }
}
