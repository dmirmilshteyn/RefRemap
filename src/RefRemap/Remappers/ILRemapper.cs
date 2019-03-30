using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Collections.Generic;
using System.Text;

namespace RefRemap.Remappers
{
    public class ILRemapper : AbstractRemapper
    {
        Stack<MethodDef> methodDefStack;

        public ILRemapper(RemapContext context) : base(context) {
            this.methodDefStack = new Stack<MethodDef>();
        }

        public override void Remap() {
            foreach (var type in EnumerateTypes(Module.Types)) {
                RemapTypeDef(type);
            }
        }

        public override bool IsCompatible() {
            return true;
        }

        private IEnumerable<TypeDef> EnumerateTypes(IEnumerable<TypeDef> types) {
            foreach (var type in types) {
                yield return type;
                foreach (var nestedType in EnumerateTypes(type.NestedTypes)) {
                    yield return nestedType;
                }
            }
        }

        private ITypeDefOrRef RemapReference(ITypeDefOrRef reference) {
            return RemapReference(reference.ToTypeSig()).ToTypeDefOrRef();
        }

        private TypeSig RemapReference(TypeSig reference) {
            if (reference == null) {
                return null;
            }

            if (reference.ToTypeDefOrRef().NumberOfGenericParameters > 0) {
                return RemapGenericType(reference);
            }

            return Import(reference);
        }

        private TypeSig RemapGenericType(TypeSig reference) {
            var typeDef = reference.TryGetTypeDef();
            if (typeDef != null) {
                if (reference.DefinitionAssembly != null && reference.DefinitionAssembly == Module.Assembly) {
                    return reference;
                }
            }

            var genericInstSig = reference.ToGenericInstSig();
            if (genericInstSig != null) {
                return RemapGenericInstSig(genericInstSig);
            }

            throw new NotImplementedException();
        }

        private TypeSig RemapGenericInstSig(GenericInstSig genericInstSig) {
            var genericTypeRef = genericInstSig.GenericType;

            var importedTypeRef = Import(genericTypeRef);

            var remappedGenericInstSig = new GenericInstSig(importedTypeRef.ToClassOrValueTypeSig(), genericInstSig.GenericArguments.Count);

            foreach (var referenceGenericArgument in genericInstSig.GenericArguments) {
                remappedGenericInstSig.GenericArguments.Add(RemapReference(referenceGenericArgument));
            }

            return remappedGenericInstSig;
        }

        private TypeSig Import(TypeSig typeSig) {
            if (typeSig.DefinitionAssembly != null && SourceAssemblies.Contains(typeSig.DefinitionAssembly.Name)) {
                var name = typeSig.FullName;
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

                var targetTypeDef = TargetModule.FindThrow(name, false);

                var importedTypeSig = Module.Import(targetTypeDef).ToTypeSig();

                if (Options.Resolve) {
                    importedTypeSig.ToTypeDefOrRef().ResolveTypeDefThrow();
                }

                if (typeSig.IsSZArray) {
                    return new SZArraySig(importedTypeSig);
                } else if (typeSig.IsByRef) {
                    return new ByRefSig(importedTypeSig);
                } else {
                    return importedTypeSig;
                }
            }

            return typeSig;
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
