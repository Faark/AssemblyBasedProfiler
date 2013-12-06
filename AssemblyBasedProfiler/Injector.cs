using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;


namespace AssemblyBasedProfiller
{

    // Todo: meth.Body.Optimize/SimplyfyMacros() have to be reconsidered / may generalized?

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
        public string File { get; private set; }
        public AssemblyDefinition Assembly { get; private set; }
        public ModuleDefinition Module { get; private set; }
        public Injector(string file)
        {
            /*var assembly_resolver = new DefaultAssemblyResolver();
            assembly_resolver.AddSearchDirectory(assembly_file.DirectoryName);
            Assembly = AssemblyDefinition.ReadAssembly(
                assembly_file.FullName,
                new ReaderParameters() { AssemblyResolver = assembly_resolver }
                );*/
            File = file;
            Assembly = AssemblyDefinition.ReadAssembly(file);
            Module = Assembly.MainModule;
        }

        public void Inject_RegisterMethods(IEnumerable<Tuple<int, MethodDefinition>> methodsToRegister, MethodDefinition methodToDoRegisteringIn)
        {
            //var meth_tokenToMethodBase = Module.Import(new Func<RuntimeMethodHandle, System.Reflection.MethodBase>(System.Reflection.MethodBase.GetMethodFromHandle).Method);
            var meth_tokenToMethodBase = Module.Import(new Func<RuntimeMethodHandle, RuntimeTypeHandle, System.Reflection.MethodBase>(System.Reflection.MethodBase.GetMethodFromHandle).Method);
            var meth_addMethodToLibrary = Module.Import(new Action<Int32, System.Reflection.MethodBase>(ProfilerLib.MethodLibrary.Register).Method);

            methodToDoRegisteringIn.Body.SimplifyMacros();

            var ilGen = methodToDoRegisteringIn.Body.GetILProcessor();

            var oldFirst = methodToDoRegisteringIn.Body.Instructions.First();

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

            methodToDoRegisteringIn.Body.OptimizeMacros();
        }        
        public void Inject_RegisterMethodsAtType(IEnumerable<Tuple<int, MethodDefinition>> methodsToRegister, TypeDefinition typeToDoRegisteringIn)
        {
            Inject_RegisterMethods(methodsToRegister, typeToDoRegisteringIn.GetStaticConstructor());
        }
        public void Inject_AddProfileCalls(int methodId, bool useLeaveEx, MethodDefinition method)
        {
            method.Body.SimplifyMacros();
            var meth_enter = Module.Import(new Action<Int32>(ProfilerLib.Profiler.Enter).Method);
            var meth_leave = Module.Import(useLeaveEx ? new Action<Int32>(ProfilerLib.Profiler.LeaveEx).Method : new Action(ProfilerLib.Profiler.Leave).Method);

            var ilGen = method.Body.GetILProcessor();

            var voidReturn = method.ReturnType.IsVoid();

            // At first we add out try{...}finally{Leave();} around everything. Ret appears to always be the last cmd, so there are 3 possible situations:
            // - return void. Easy, just end the finally block right in front of this ret.
            // - ldloc; return; Same as above, but this time we have to end it before both commands (turns out there might be jumps to ret directly, that have to be caught as well!)
            // - return value without ldloc... this is tricky. I might have to create a new local and store it in there....
            Instruction firstInstructionAfterProfiling;
            VariableDefinition localToStoreResult = null;
            /*if (method.Name == "get_SignalProcessor" && method.DeclaringType.Name == "VesselSatellite")
            {
                Console.WriteLine("gSP");
            }*/
            if (voidReturn)
            {
                firstInstructionAfterProfiling = method.Body.Instructions.Last();
            }
            else if (
               (method.Body.Instructions.Count >= 2) &&
               (new[]{
                    OpCodes.Ldloc, 
                    OpCodes.Ldloc_0, 
                    OpCodes.Ldloc_1, 
                    OpCodes.Ldloc_2, 
                    OpCodes.Ldloc_3, 
                    OpCodes.Ldloc_S
                }.Contains(method.Body.Instructions.Reverse().Skip(1).First().OpCode)) &&
               !method.Body.Instructions.Any(instr => instr.Operand == method.Body.Instructions.Last())
               )
            {
                firstInstructionAfterProfiling = method.Body.Instructions.Reverse().Skip(1).First();
            }
            else
            {
                localToStoreResult = new VariableDefinition(method.ReturnType);
                method.Body.Variables.Add(localToStoreResult);
                var cpy = method.Body.Instructions.Last().ReplaceBy(OpCodes.Stloc, localToStoreResult);
                ilGen.InsertAfter(
                    method.Body.Instructions.Last(),
                    firstInstructionAfterProfiling = ilGen.Create(OpCodes.Ldloc, localToStoreResult),
                    ilGen.Create(OpCodes.Ret)
                    );
                //throw new NotImplementedException("Difficult case not yet implemented. Though happily the compiler tend to not use it anyway :) ");
            }
            // problem: lastInstruction is most likely used as jump target. So we better not move it but instead re-moddle it into a leave and re-add the last instruction



            //Instruction lastInstr = null;
            foreach (var instr in method.Body.Instructions.TakeWhile(el => el != firstInstructionAfterProfiling).ToList())
            {
                // assumption (verify?): There can only be rets outside of any ExceptionHandlers, since they would require a leave!
                // anyway, if there isn't already an return local set we have to inject it...
                if (instr.OpCode == OpCodes.Ret)
                {
                    var retCmd = instr;
                    if (!voidReturn)
                    {
                        if (localToStoreResult == null)
                        {
                            localToStoreResult = new VariableDefinition(method.ReturnType);
                            method.Body.Variables.Add(localToStoreResult);
                            ilGen.InsertBefore(
                                method.Body.Instructions.Last(),
                                ilGen.Create(OpCodes.Stloc, localToStoreResult),
                                firstInstructionAfterProfiling = ilGen.Create(OpCodes.Ldloc, localToStoreResult)
                                );
                        }
                        var cpy = instr.ReplaceBy(OpCodes.Stloc, localToStoreResult);
                        ilGen.InsertAfter(instr, cpy);
                        retCmd = cpy;
                    }
                    //ilGen.InsertBefore(instr, ilGen.Create(OpCodes.Stloc, localToStoreResult));
                    retCmd.OpCode = OpCodes.Br; // Todo: make this "leave ACTUALFIRSTCMD AFTER BLOCK"
                    retCmd.Operand = firstInstructionAfterProfiling;
                }
            }

            Instruction readdedFirstInstructionAfterHandler = firstInstructionAfterProfiling.Clone();
            ilGen.InsertAfter(firstInstructionAfterProfiling, readdedFirstInstructionAfterHandler);
            firstInstructionAfterProfiling.OpCode = OpCodes.Leave;
            firstInstructionAfterProfiling.Operand = readdedFirstInstructionAfterHandler;
            Instruction handlerFirstInstruction;
            if (useLeaveEx)
            {
                ilGen.InsertAfter(firstInstructionAfterProfiling,
                    handlerFirstInstruction = ilGen.Create(OpCodes.Ldc_I4, methodId),
                    ilGen.Create(OpCodes.Call, meth_leave),
                    ilGen.Create(OpCodes.Endfinally)
                    );
            }
            else
            {
                ilGen.InsertAfter(firstInstructionAfterProfiling,
                    handlerFirstInstruction = ilGen.Create(OpCodes.Call, meth_leave),
                    ilGen.Create(OpCodes.Endfinally)
                    );
            }

            var handler = new ExceptionHandler(ExceptionHandlerType.Finally)
            {
                TryStart = method.Body.Instructions.First(),
                TryEnd = handlerFirstInstruction,
                HandlerStart = handlerFirstInstruction,
                HandlerEnd = readdedFirstInstructionAfterHandler
            };
            method.Body.ExceptionHandlers.Add(handler);

            ilGen.InsertBefore(
                method.Body.Instructions.First(),
                //ilGen.Create(OpCodes.Ldftn, method),
                ilGen.Create(OpCodes.Ldc_I4, methodId),
                ilGen.Create(OpCodes.Call, meth_enter)
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
                System.IO.File.Copy(File, File + ".backup", true);
            }
            Assembly.Write(File);
        }
    }
}
