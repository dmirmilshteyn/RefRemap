using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Text;

namespace RefRemap
{
    public class RemapContext
    {
        private readonly ModuleDefMD module;
        private readonly ModuleDefMD targetModule;
        private readonly HashSet<string> sourceAssemblies;

        Stack<MethodDef> methodDefStack;

        public RemapContext(ModuleDefMD module, ModuleDefMD targetModule, HashSet<string> sourceAssemblies) {
            this.module = module;
            this.targetModule = targetModule;
            this.sourceAssemblies = sourceAssemblies;

            this.methodDefStack = new Stack<MethodDef>();
        }

        public void Remap() {
            foreach (var type in EnumerateTypes(module.Types)) {
                RemapTypeDef(type);
            }
        }

        private IEnumerable<TypeDef> EnumerateTypes(IEnumerable<TypeDef> types) {
            foreach (var type in types) {
                yield return type;
                foreach (var nestedType in EnumerateTypes(type.NestedTypes)) {
                    yield return nestedType;
                }
            }
        }

        private TypeSig RemapReference(TypeSig reference) {
            return RemapReference(reference.ToTypeDefOrRef()).ToTypeSig();
        }

        private ITypeDefOrRef RemapReference(ITypeDefOrRef reference) {
            if (reference == null) {
                return null;
            }

            if (reference.NumberOfGenericParameters > 0) {
                return RemapGenericType(reference);
            }

            return Import(reference);
        }

        private ITypeDefOrRef RemapGenericType(ITypeDefOrRef reference) {
            if (reference.IsTypeDef) {
                if (reference.DefinitionAssembly != null && reference.DefinitionAssembly == module.Assembly) {
                    return reference;
                }
            } else if (reference.IsTypeSpec || reference.IsTypeRef) {
                return RemapGenericInstSig(reference.TryGetGenericInstSig());
            }

            throw new NotImplementedException();
        }

        private ITypeDefOrRef RemapGenericInstSig(GenericInstSig genericInstSig) {
            var genericTypeRef = genericInstSig.GenericType.ToTypeDefOrRef();

            var importedTypeRef = Import(genericTypeRef);

            var remappedGenericInstSig = new GenericInstSig(new ClassSig(importedTypeRef), genericInstSig.GenericArguments.Count);

            foreach (var referenceGenericArgument in genericInstSig.GenericArguments) {
                var genericArgumentRef = referenceGenericArgument.ToTypeDefOrRef();

                remappedGenericInstSig.GenericArguments.Add(RemapReference(genericArgumentRef).ToTypeSig());
            }

            return remappedGenericInstSig.ToTypeDefOrRef();
        }

        private ITypeDefOrRef Import(ITypeDefOrRef reference) {
            if (reference.DefinitionAssembly != null && sourceAssemblies.Contains(reference.DefinitionAssembly.Name)) {
                var typeSig = reference.ToTypeSig();

                var name = reference.FullName;
                if (typeSig.IsSZArray) {
                    // Trim the [] at the end of the name
                    // TODO: Is this the correct way to do the lookup?
                    name = name.Substring(0, name.Length - 2);
                } else if (typeSig.IsByRef) {
                    // Trim the & at the end of the name
                    // TODO: Is this the correct way to do the lookup?
                    name = name.Substring(0, name.Length - 1);
                } else if (typeSig.IsFunctionPointer || typeSig.IsPointer || typeSig.IsPinned || typeSig.IsModuleSig || typeSig.IsGenericParameter ||
                           typeSig.IsGenericTypeParameter || typeSig.IsGenericInstanceType || typeSig.IsGenericMethodParameter) {
                    throw new NotImplementedException();
                }

                var targetTypeDef = targetModule.FindThrow(name, false);

                var importedTypeSig = module.Import(targetTypeDef.ToTypeSig());

                importedTypeSig.ToTypeDefOrRef().ResolveTypeDefThrow();

                if (typeSig.IsSZArray) {
                    return new SZArraySig(importedTypeSig).ToTypeDefOrRef();
                } else if (typeSig.IsByRef) {
                    return new ByRefSig(importedTypeSig).ToTypeDefOrRef();
                } else {
                    return importedTypeSig.ToTypeDefOrRef();
                }
            }

            return reference;
        }

        private void RemapTypeDef(TypeDef type) {
            RemapCustomAttributes(type.CustomAttributes);

            foreach (var genericParameter in type.GenericParameters) {
                foreach (var constraint in genericParameter.GenericParamConstraints) {
                    constraint.Constraint = RemapReference(constraint.Constraint);
                }
            }

            type.BaseType = RemapReference(type.BaseType);

            foreach (var typeInterface in type.Interfaces) {
                typeInterface.Interface = RemapReference(typeInterface.Interface);
            }

            foreach (var property in type.Properties) {
                RemapPropertyDef(property);
            }

            foreach (var method in type.Methods) {
                RemapMethodDef(method);
            }
        }

        private void RemapMethodDef(MethodDef method) {
            // Avoid mapping recursive method calls
            if (methodDefStack.Contains(method)) {
                return;
            }
            methodDefStack.Push(method);

            RemapCustomAttributes(method.CustomAttributes);

            RemapMethodSig(method.MethodSig);
            if (method.HasReturnType) {
                method.ReturnType = RemapReference(method.ReturnType);
            }

            foreach (var parameter in method.Parameters) {
                parameter.Type = RemapReference(parameter.Type);
            }

            foreach (var methodOverride in method.Overrides) {
                RemapMethodDefOrRef(methodOverride.MethodBody);
                RemapMethodDefOrRef(methodOverride.MethodDeclaration);
            }

            if (method.HasBody) {
                var body = method.Body;

                foreach (var local in body.Variables) {
                    local.Type = RemapReference(local.Type);
                }

                foreach (var instruction in body.Instructions) {
                    // Logic for this section taken from:
                    // https://github.com/gluck/il-repack/blob/master/ILRepack/RepackImporter.cs#L453
                    // Licensed under Apache 2

                    if (instruction.OpCode.Code != Code.Calli) {
                        switch (instruction.OpCode.OperandType) {
                            case OperandType.InlineMethod:
                            case OperandType.InlineType:
                            case OperandType.InlineTok:
                            case OperandType.InlineField: {
                                    RemapInstruction(instruction);
                                }
                                break;
                        }
                    }
                }
            }

            methodDefStack.Pop();
        }

        private void RemapMethodSig(MethodSig methodSig) {
            methodSig.RetType = RemapReference(methodSig.RetType);

            for (var i = 0; i < methodSig.Params.Count; i++) {
                methodSig.Params[i] = RemapReference(methodSig.Params[i]);
            }
        }

        private void RemapPropertyDef(PropertyDef property) {
            RemapCustomAttributes(property.CustomAttributes);

            foreach (var method in property.GetMethods) {
                RemapMethodDef(method);
            }
            foreach (var method in property.SetMethods) {
                RemapMethodDef(method);
            }
            foreach (var method in property.OtherMethods) {
                RemapMethodDef(method);
            }

            RemapPropertySig(property.PropertySig);
        }

        private void RemapPropertySig(PropertySig propertySig) {
            propertySig.RetType = RemapReference(propertySig.RetType);

            for (var i = 0; i < propertySig.Params.Count; i++) {
                propertySig.Params[i] = RemapReference(propertySig.Params[i]);
            }
        }

        private void RemapInstruction(Instruction instruction) {
            switch (instruction.Operand) {
                case FieldDef fieldDef: {
                        RemapFieldDef(fieldDef);
                    }
                    break;
                case MemberRef memberRef: {
                        RemapMemberRef(memberRef);
                    }
                    break;
                case MethodSpec methodSpec: {
                        RemapGenericInstMethodSig(methodSpec.GenericInstMethodSig);
                        RemapMethodDefOrRef(methodSpec.Method);
                    }
                    break;
                case MethodDef methodDef: {
                        RemapMethodDef(methodDef);
                    }
                    break;
                case TypeRef typeRef: {
                        instruction.Operand = RemapReference(typeRef);
                    }
                    break;
                case TypeDef typeDef: {
                        // Do nothing? Typedefs are already remapped in another area
                    }
                    break;
                case TypeSpec typeSpec: {
                        typeSpec.TypeSig = RemapReference(typeSpec.TypeSig);
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void RemapMethodDefOrRef(IMethodDefOrRef methodDefOrRef) {
            if (methodDefOrRef.IsMemberRef) {
                RemapMemberRef((MemberRef)methodDefOrRef);
            } else if (methodDefOrRef.IsMethodDef) {
                RemapMethodDef((MethodDef)methodDefOrRef);
            } else {
                throw new NotImplementedException();
            }
        }

        private void RemapFieldDef(FieldDef fieldDef) {
            RemapCustomAttributes(fieldDef.CustomAttributes);

            fieldDef.FieldType = RemapReference(fieldDef.FieldType);
        }

        private void RemapGenericInstMethodSig(GenericInstMethodSig genericInstMethodSig) {
            for (var i = 0; i < genericInstMethodSig.GenericArguments.Count; i++) {
                genericInstMethodSig.GenericArguments[i] = RemapReference(genericInstMethodSig.GenericArguments[i]);
            }
        }

        private void RemapMemberRef(MemberRef memberRef) {
            RemapCustomAttributes(memberRef.CustomAttributes);

            var classReference = (ITypeDefOrRef)memberRef.Class;

            memberRef.Class = RemapReference(classReference);
            memberRef.ReturnType = RemapReference(memberRef.ReturnType);

            if (memberRef.IsFieldRef) {
                memberRef.FieldSig.Type = RemapReference(memberRef.FieldSig.Type);
            }
            if (memberRef.IsMethodRef) {
                RemapMethodSig(memberRef.MethodSig);
            }
        }

        private void RemapCustomAttributes(CustomAttributeCollection customAttributes) {
            foreach (var customAttribute in customAttributes) {
                var ctor = customAttribute.Constructor;

                if (ctor.IsMemberRef) {
                    RemapMemberRef((MemberRef)ctor);
                } else {
                    throw new NotImplementedException();
                }

                for (var i = customAttribute.ConstructorArguments.Count - 1; i >= 0; i--) {
                    var caArgument = customAttribute.ConstructorArguments[i];

                    var modifiedCAArgument = new CAArgument(RemapReference(caArgument.Type), caArgument.Value);

                    customAttribute.ConstructorArguments.RemoveAt(i);
                    customAttribute.ConstructorArguments.Insert(i, modifiedCAArgument);
                }
            }
        }
    }
}
