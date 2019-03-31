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

// Portions taken from ILRepack
// https://github.com/gluck/il-repack

//
// Copyright (c) 2015 Timotei Dolean
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using Confuser.Renamer.BAML;
using dnlib.DotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;

namespace RefRemap.Remappers
{
    public class WPFRemapper : AbstractRemapper
    {
        private static readonly Regex ResourceNamePattern = new Regex("^.*\\.g\\.resources$");

        private static string XmlnsAssemblyDefinition = "assembly=";

        public WPFRemapper(RemapContext context) : base(context) {
        }

        public override void Remap() {
            var documents = ReadDocuments();

            foreach (var document in documents) {
                ProcessDocument(document);
            }

            WriteDocuments(documents);
        }

        public override bool IsCompatible() {
            foreach (var assembly in Module.GetAssemblyRefs()) {
                if (assembly.Name == "WindowsBase" || assembly.Name == "PresentationCore" || assembly.Name == "PresentationFramework" || assembly.Name == "System.Xaml") {
                    return true;
                }
            }

            return false;
        }

        private List<BamlDocument> ReadDocuments() {
            var documents = new List<BamlDocument>();

            foreach (var resource in Module.Resources.OfType<EmbeddedResource>()) {
                var match = ResourceNamePattern.Match(resource.Name);
                if (!match.Success) {
                    continue;
                }

                using (var resourceStream = resource.CreateReader().AsStream()) {
                    using (var resourceReader = new ResourceReader(resourceStream)) {
                        var enumerator = resourceReader.GetEnumerator();
                        while (enumerator.MoveNext()) {
                            var name = (string)enumerator.Key;
                            if (!name.EndsWith(".baml")) {
                                continue;
                            }

                            resourceReader.GetResourceData(name, out var typeName, out var data);

                            using (var bamlStream = new MemoryStream(data, 4, data.Length - 4)) {
                                var document = BamlReader.ReadDocument(bamlStream);
                                document.DocumentName = name;

                                documents.Add(document);
                            }
                        }
                    }
                }
            }

            return documents;
        }

        private void WriteDocuments(List<BamlDocument> documents) {
            var indexedDocuments = documents.ToDictionary(x => x.DocumentName);
            var newResources = new List<EmbeddedResource>();

            foreach (var resource in Module.Resources.OfType<EmbeddedResource>()) {
                using (var stream = new MemoryStream()) {
                    using (var resourceWriter = new ResourceWriter(stream)) {
                        using (var resourceStream = resource.CreateReader().AsStream()) {
                            using (var resourceReader = new ResourceReader(resourceStream)) {
                                var enumerator = resourceReader.GetEnumerator();
                                while (enumerator.MoveNext()) {
                                    var name = (string)enumerator.Key;

                                    resourceReader.GetResourceData(name, out var typeName, out var data);

                                    if (indexedDocuments.TryGetValue(name, out var document)) {
                                        using (var documentStream = new MemoryStream()) {
                                            documentStream.Position = 4;
                                            BamlWriter.WriteDocument(document, documentStream);
                                            documentStream.Position = 0;
                                            documentStream.Write(BitConverter.GetBytes((int)documentStream.Length - 4), 0, 4);
                                            data = documentStream.ToArray();
                                        }
                                    }

                                    resourceWriter.AddResourceData(name, typeName, data);
                                }
                            }
                        }

                        resourceWriter.Generate();
                        newResources.Add(new EmbeddedResource(resource.Name, stream.ToArray(), resource.Attributes));
                    }
                }
            }

            foreach (var resource in newResources) {
                var index = Module.Resources.IndexOfEmbeddedResource(resource.Name);

                Module.Resources[index] = resource;
            }
        }

        private void ProcessDocument(BamlDocument document) {
            foreach (var record in document) {
                switch (record) {
                    case AssemblyInfoRecord assemblyInfoRecord: {
                            ProcessRecord(assemblyInfoRecord);
                        }
                        break;
                    case XmlnsPropertyRecord xmlnsPropertyRecord: {
                            ProcessRecord(xmlnsPropertyRecord);
                        }
                        break;
                    case PropertyWithConverterRecord propertyWithConverterRecord: {
                            ProcessRecord(propertyWithConverterRecord);
                        }
                        break;
                    case TextWithConverterRecord textWithConverterRecord: {
                            ProcessRecord(textWithConverterRecord);
                        }
                        break;
                    case TypeInfoRecord typeInfoRecord: {
                            ProcessRecord(typeInfoRecord);
                        }
                        break;
                }
            }
        }

        private void ProcessRecord(AssemblyInfoRecord record) {
            var assemblyName = new System.Reflection.AssemblyName(record.AssemblyFullName);

            if (SourceAssemblies.Contains(assemblyName.Name)) {
                record.AssemblyFullName = TargetModule.Assembly.FullName;
            }
        }

        private void ProcessRecord(XmlnsPropertyRecord record) {
            string xmlNamespace = record.XmlNamespace;
            int assemblyStart = xmlNamespace.IndexOf(XmlnsAssemblyDefinition, StringComparison.Ordinal);
            if (assemblyStart == -1)
                return;

            // Make sure it is one of the merged assemblies
            string xmlAssembly = xmlNamespace.Substring(assemblyStart + XmlnsAssemblyDefinition.Length);
            if (!SourceAssemblies.Contains(xmlAssembly)) {
                return;
            }

            string xmlNsWithoutAssembly = xmlNamespace.Substring(0, assemblyStart);
            record.XmlNamespace = $"{xmlNsWithoutAssembly}{XmlnsAssemblyDefinition}{TargetModule.Assembly.Name}";
        }

        private void ProcessRecord(PropertyWithConverterRecord record) {
            record.Value = PatchPath(record.Value);
        }

        private void ProcessRecord(TextWithConverterRecord record) {
            record.Value = PatchPath(record.Value);
        }

        private void ProcessRecord(TypeInfoRecord record) {
            if (record.TypeFullName.Contains("[[")) {
                throw new NotImplementedException();
            }
        }

        private string PatchPath(string path) {
            if (string.IsNullOrEmpty(path) || !(path.StartsWith("/") || path.StartsWith("pack://"))) {
                return path;
            }

            string patchedPath;
            foreach (var sourceAssembly in SourceAssemblies) {
                if (TryPatchPath(path, TargetModule.Assembly.Name, sourceAssembly, out patchedPath)) {
                    return patchedPath;
                }
            }

            if (TryPatchPath(path, TargetModule.Assembly.Name, Module.Assembly.Name, out patchedPath)) {
                return patchedPath;
            }

            return path;
        }

        private bool TryPatchPath(string path, string primaryAssemblyName, string referenceAssemblyName, out string patchedPath) {
            string referenceAssemblyPath = GetAssemblyPath(referenceAssemblyName);
            string newPath = GetAssemblyPath(primaryAssemblyName) + "/" + referenceAssemblyName;

            // /library;component/file.xaml -> /primary;component/library/file.xaml
            patchedPath = path.Replace(referenceAssemblyPath, newPath);

            // if they're modified, we're good!
            return !ReferenceEquals(patchedPath, path);
        }

        private string GetAssemblyPath(string assemblyName) {
            return $"/{assemblyName};component";
        }
    }
}
