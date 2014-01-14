using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;


namespace AssemblyBasedProfiller
{
    public static class InjectorExtensions
    {
        public static void InsertBefore(this ILProcessor self, Instruction before, IEnumerable<Instruction> instructions)
        {
            foreach (var i in instructions)
            {
                self.InsertBefore(before, i);
            }
        }
        public static void InsertBefore(this ILProcessor self, Instruction before, params Instruction[] instructions)
        {
            self.InsertBefore(before, (IEnumerable<Instruction>)instructions);
        }
        public static void InsertAfter(this ILProcessor self, Instruction after_target, IEnumerable<Instruction> instructions)
        {
            foreach (var i in instructions)
            {
                self.InsertAfter(after_target, i);
                after_target = i;
            }
        }
        public static void InsertAfter(this ILProcessor self, Instruction after_target, params Instruction[] instructions)
        {
            self.InsertAfter(after_target, (IEnumerable<Instruction>)instructions);
        }

        public static MethodDefinition GetStaticConstructor(this TypeDefinition type)
        {
            var cctor = type.Methods.FirstOrDefault(meth => meth.Name == ".cctor");
            if (cctor == null)
            {
                var voidRef = type.Module.Import(typeof(void));
                var attributes = MethodAttributes.Static
                                | MethodAttributes.SpecialName
                                | MethodAttributes.RTSpecialName;
                cctor = new MethodDefinition(".cctor", attributes, voidRef);
                cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
                type.Methods.Add(cctor);
            }
            return cctor;
        }
        public static MethodDefinition GetStaticConstructor(this ModuleDefinition self)
        {
            return self.Types.Single(t => t.Name == "<Module>").GetStaticConstructor();
        }

        public static Type ToType(this TypeReference self)
        {
            /*
             * Warning: The following method is reused from an old project. No idea how reliable it is...
             * 
             * Also this method cant handle types of non-loadeed assemblies!
             */


            var type_name = self.FullName;
            // Todo: FullName should be wrong for lots of classes (nested eg). Find a better solution, maybe via token, like cecil does?
            if (self.IsGenericInstance)
            {
                var git = self as GenericInstanceType;
                var ungeneric_type = self.GetElementType().ToType();
                var generic_args = new Type[git.GenericArguments.Count];
                for (var i = 0; i < git.GenericArguments.Count; i++)
                {
                    generic_args[i] = git.GenericArguments[i].ToType();
                    // Todo: recursions could be possible?
                }
                return ungeneric_type.MakeGenericType(generic_args);
            }
            if (self.IsGenericParameter)
            {
                throw new Exception("Generic params not yet tested, sry. Pls leave me a msg.");
            }
            if (self.IsArray)
            {
                var at = self as ArrayType;
                var elementType = at.ElementType.ToType();
                return elementType.MakeArrayType(at.Rank);
            }
            if (self.IsByReference)
            {
                throw new Exception("ByRef not yet tested, sry. Pls leave me a msg.");
            }
            if (self.IsNested)
            {
                //dont think this solution is... "Perfect"
                type_name = type_name.Replace('/', '+');
                //throw new Exception("Nested classes are not yet supported, sry. Pls leave me a msg.");
            }
            /*
             * Token wont work, since we wont get the actual token without using Resolve() first.... :/ 
            var ass = System.Reflection.Assembly./*ReflectionOnly*Load((self.Scope as AssemblyNameReference).ToString());
            var t = ass.GetModules().Select(mod => mod.ResolveType((int)self.MetadataToken.RID)).Single(el => el != null);
            if (t.Name != self.Name)
            {
                throw new Exception("Failed to resolve type.");
            }*/

            var assembly = self.Scope as AssemblyNameReference;
            if (self.Scope is ModuleDefinition)
            {
                assembly = (self.Scope as ModuleDefinition).Assembly.Name;
            }

            var t = Type.GetType(System.Reflection.Assembly.CreateQualifiedName(assembly.FullName, type_name), true);
            /*if( t.MetadataToken != self.MetadataToken.RID ){
                throw new Exception("Failed to resolve type, token does not match.");
            }*/
            return t;
        }
        public static bool IsVoid(this TypeReference self)
        {
            return self.FullName == "System.Void" && self.MetadataType == MetadataType.Void;
        }

        public static Instruction Clone(this Instruction self)
        {
            var newInstr = Instruction.Create(OpCodes.Ret);
            newInstr.OpCode = self.OpCode;
            newInstr.Operand = self.Operand;
            return newInstr;
        }
        /// <summary>
        /// Replaces a command with another command, but returns a copy of the original one.
        /// </summary>
        /// <param name="self"></param>
        /// <param name="newCommand"></param>
        /// <param name="newOperand"></param>
        /// <returns></returns>
        public static Instruction ReplaceBy(this Instruction self, OpCode newCommand, object newOperand)
        {
            var cpy = self.Clone();
            self.OpCode = newCommand;
            self.Operand = newOperand;
            return cpy;
        }
    }
    /// <summary>
    /// This class takes care of the actual IL manipulation.
    /// </summary>
    public class Injector
    {
        public System.IO.FileInfo File { get; private set; }
        public string BackupFile { get { return File.FullName + ".backup"; } }
        public AssemblyDefinition Assembly { get; private set; }
        public ModuleDefinition Module { get; private set; }
        protected MethodReference Method_ProfilerEnter;
        protected MethodReference Method_ProfilerLeave;
        protected MethodReference Method_ProfilerLeaveEx;
        public Injector(System.IO.FileInfo file)
        {
            /*var assembly_resolver = new DefaultAssemblyResolver();
            assembly_resolver.AddSearchDirectory(assembly_file.DirectoryName);
            Assembly = AssemblyDefinition.ReadAssembly(
                assembly_file.FullName,
                new ReaderParameters() { AssemblyResolver = assembly_resolver }
                );*/
            File = file;
            Assembly = AssemblyDefinition.ReadAssembly(file.FullName);
            Module = Assembly.MainModule;


            Method_ProfilerEnter = Module.Import(new Action<Int32>(ProfilerLib.Profiler.Enter).Method);
            Method_ProfilerLeave = Module.Import(new Action(ProfilerLib.Profiler.Leave).Method);
            Method_ProfilerLeaveEx = Module.Import(new Action<Int32>(ProfilerLib.Profiler.LeaveEx).Method);
        }

        public bool Check_HasModifiedMarker()
        {
            return Module.GetStaticConstructor().DeclaringType.Fields.Any(f => f.Name == "IsProfilingAssembly" && f.IsStatic == false && f.FieldType.FullName == "System.Boolean");
        }
        public void Inject_ModifiedMarker()
        {
            Module.GetStaticConstructor().DeclaringType.Fields.Add(new FieldDefinition("IsProfilingAssembly", FieldAttributes.Public, Module.Import(typeof(bool))));
        }

        public void Inject_RegisterMethods(IEnumerable<Tuple<int, MethodDefinition>> methodsToRegister, MethodDefinition methodToPlaceRegisteringIn)
        {
            //var meth_tokenToMethodBase = Module.Import(new Func<RuntimeMethodHandle, System.Reflection.MethodBase>(System.Reflection.MethodBase.GetMethodFromHandle).Method);
            var meth_tokenToMethodBase = Module.Import(new Func<RuntimeMethodHandle, RuntimeTypeHandle, System.Reflection.MethodBase>(System.Reflection.MethodBase.GetMethodFromHandle).Method);
            var meth_addMethodToLibrary = Module.Import(new Action<Int32, System.Reflection.MethodBase>(ProfilerLib.MethodLibrary.Register).Method);

            methodToPlaceRegisteringIn.Body.SimplifyMacros();

            var ilGen = methodToPlaceRegisteringIn.Body.GetILProcessor();

            var oldFirst = methodToPlaceRegisteringIn.Body.Instructions.First();

            foreach (var meth in methodsToRegister)
            {
                ilGen.InsertBefore(
                    oldFirst,
                    //ilGen.Create(OpCodes.Ldftn, mdef),
                    ilGen.Create(OpCodes.Ldc_I4, meth.Item1),
                    ilGen.Create(OpCodes.Ldtoken, meth.Item2),
                    ilGen.Create(OpCodes.Ldtoken, meth.Item2.DeclaringType),
                    ilGen.Create(OpCodes.Call, meth_tokenToMethodBase),
                    ilGen.Create(OpCodes.Call, meth_addMethodToLibrary)
                    );
            }

            methodToPlaceRegisteringIn.Body.OptimizeMacros();
        }        
        public void Inject_RegisterMethodsAtType(IEnumerable<Tuple<int, MethodDefinition>> methodsToRegister, TypeDefinition typeToPlaceRegisteringIn)
        {
            Inject_RegisterMethods(methodsToRegister, typeToPlaceRegisteringIn.GetStaticConstructor());
        }

        /// <summary>
        /// Wrapping a method in try{...}finally{Leave();} consists of quite a few challanges... this class takes care of them
        /// </summary>
        class ExceptionHandlerWrapper
        {
            MethodDefinition method;
            ILProcessor ilGen;

            Instruction FirstInstructionAfterWrappedCode;
            public ExceptionHandlerWrapper(MethodDefinition method)
            {
                this.method = method;
                this.ilGen = method.Body.GetILProcessor();
            }

            public void PrepareAndGetLastWrappedInstruction()
            {
                VariableDefinition retStorage = null;
                foreach (var instruction in method.Body.Instructions.ToList())
                {
                    if (instruction.OpCode == OpCodes.Ret)
                    {
                        if (FirstInstructionAfterWrappedCode == null)
                        {
                            if (method.ReturnType.IsVoid())
                            {
                                ilGen.InsertAfter(
                                    method.Body.Instructions.Last(),
                                    FirstInstructionAfterWrappedCode = ilGen.Create(OpCodes.Ret)
                                    );
                            }
                            else
                            {
                                retStorage = new VariableDefinition(method.ReturnType);
                                method.Body.Variables.Add(retStorage);
                                ilGen.InsertAfter(
                                    method.Body.Instructions.Last(),
                                    FirstInstructionAfterWrappedCode = ilGen.Create(OpCodes.Ldloc, retStorage),
                                    ilGen.Create(OpCodes.Ret)
                                    );
                            }
                        }
                        if (method.ReturnType.IsVoid())
                        {
                            instruction.OpCode = OpCodes.Leave;
                            instruction.Operand = FirstInstructionAfterWrappedCode;
                        }
                        else
                        {
                            instruction.OpCode = OpCodes.Stloc;
                            instruction.Operand = retStorage;
                            ilGen.InsertAfter(instruction, ilGen.Create(OpCodes.Leave, FirstInstructionAfterWrappedCode));
                        }
                    }
                }
            }
            public void DoActualWrapping(params Instruction[] handlerCode)
            {
                if (FirstInstructionAfterWrappedCode == null)
                {
                    foreach (var instruction in handlerCode)
                    {
                        ilGen.Append(instruction);
                    }
                    ilGen.Append(ilGen.Create(OpCodes.Endfinally));
                }
                else
                {
                    ilGen.InsertBefore(FirstInstructionAfterWrappedCode, handlerCode);
                    ilGen.InsertBefore(FirstInstructionAfterWrappedCode, ilGen.Create(OpCodes.Endfinally));
                }

                foreach (var oldHandler in method.Body.ExceptionHandlers)
                {
                    if (oldHandler.TryEnd == null)
                        oldHandler.TryEnd = handlerCode.First();
                    if (oldHandler.HandlerEnd == null)
                        oldHandler.HandlerEnd = handlerCode.First();
                }
                var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
                {
                    TryStart = method.Body.Instructions.First(),
                    TryEnd = handlerCode.First(),
                    HandlerStart = handlerCode.First(),
                    HandlerEnd = FirstInstructionAfterWrappedCode
                };
                method.Body.ExceptionHandlers.Add(handler);
            }
        }
        public void Inject_AddProfileCalls(int methodId, bool useLeaveEx, MethodDefinition method)
        {
            if (!method.Body.Instructions.Any())
            {
                return;
            }

            method.Body.SimplifyMacros();
            var ilGen = method.Body.GetILProcessor();

            var modifier = new ExceptionHandlerWrapper(method);
            
            modifier.PrepareAndGetLastWrappedInstruction();

            if (useLeaveEx)
                modifier.DoActualWrapping(ilGen.Create(OpCodes.Ldc_I4, methodId), ilGen.Create(OpCodes.Call, Method_ProfilerLeaveEx));
            else
                modifier.DoActualWrapping(ilGen.Create(OpCodes.Call, Method_ProfilerLeave));

            ilGen.InsertBefore(
                method.Body.Instructions.First(),
                ilGen.Create(OpCodes.Ldc_I4, methodId),
                ilGen.Create(OpCodes.Call, Method_ProfilerEnter)
                );
            
            method.Body.OptimizeMacros();
        }

        public void Inject_SetupAutoSaving(int delay, string loc)
        {
            var con = Module.GetStaticConstructor();
            con.Body.SimplifyMacros();
            var ilGen = con.Body.GetILProcessor();
            ilGen.InsertBefore(
                con.Body.Instructions.First(),
                ilGen.Create(OpCodes.Ldc_I4, delay),
                ilGen.Create(OpCodes.Ldstr, loc),
                ilGen.Create(OpCodes.Call, Module.Import(new Action<int, string>(ProfilerLib.Profiler.SetAutoDataSaving).Method))
                );
            con.Body.OptimizeMacros();
        }

        public void SaveAssembly(bool create_backup)
        {
            if (create_backup)
            {
                File.CopyTo(BackupFile, true);
            }
            Assembly.Write(File.FullName);
        }
    }
}
