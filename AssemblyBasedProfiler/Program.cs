using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;

namespace AssemblyBasedProfiller
{
    class Program
    {
        static void LoadEmbededAssembly(String filename)
        {
            var data = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename);
            AppDomain.CurrentDomain.Load(new System.IO.BinaryReader(data).ReadBytes((int)data.Length));
        }
        static void Main(string[] args)
        {
            //var fullArg = args.Length > 0 ? args.Aggregate((a, b) => a + b).Trim() : "";
            var fullArg = @"C:\ksp_rt\GameData\RemoteTech2\Plugins\RemoteTech2.dll";
            //var fullArg = @"C:\ksp_rtOld\GameData\MechJeb2\Plugins\MechJeb2.dll";
            //var fullArg = @"C:\Users\Faark\Documents\GitHub\AssemblyBasedProfiler\TestApp\bin\Debug\TestLib.dll";
            var autoSaveEvery = 30;
            var autoSaveTo = "profiling.txt";
            var verifyLeaves = false;
            var methodIdRangeMin = int.MinValue;
            var methodIdRangeMax = int.MaxValue;


            if (fullArg != "" && !System.IO.File.Exists(fullArg))
            {
                Console.WriteLine("Specified file does not exist: " + fullArg);
                Console.WriteLine();
                fullArg = "";
            }
            if (fullArg == "")
            {
                // todo: Write and output a proper doc.
                Console.WriteLine("Usage: ProgName.exe fileToManipulate");
                return;
            }

            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.Rocks.dll");
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);


            // Check whether this assembly is already manipulated. Marker might be a specific method?
            // Todo: Implement this later. Cant have enough profiling for now^^

            var injector = new Injector(fullArg);
            var rand = new System.Random();
            var sets = new HashSet<int>();
            // Get a collection of all types. For its members we have to 1. do performance injection 2. add it to a local init-list or global-init. 3. Process local list
            //var cctors = new List<Mono.Cecil.MethodDefinition>();
            foreach (var type in injector.Module.GetTypes())
            {
                Console.WriteLine("Processing Type: " + type.FullName);
                var locals = new List<Tuple<int, Mono.Cecil.MethodDefinition>>();
                foreach (var meth in type.Methods)
                {
                    if (!meth.HasBody)
                        continue;

                    int methodId;
                    do
                    {
                        methodId = rand.Next(methodIdRangeMin, methodIdRangeMax);
                    } while (!sets.Add(methodId));

                    Console.WriteLine("  Method: " + meth.Name);
                    if (meth.IsConstructor && meth.IsStatic)
                    {
                        // Permission issues... => can't profilie it :(

                        //cctors.Add(meth);
                        //injector.Inject_AddProfileCalls(meth);

                    }
                    else
                    {
                        injector.Inject_AddProfileCalls(methodId, verifyLeaves, meth);
                        locals.Add(Tuple.Create(methodId, meth));
                    }
                }
                injector.Inject_RegisterMethodsAtType(locals, type);
            }

            if (autoSaveEvery > 0)
            {
                injector.Inject_SetupAutoSaving(autoSaveEvery, autoSaveTo);
            }
            Console.WriteLine("Finalizing.");
            //injector.Inject_RegisterPrimaryMethods(cctors);

            injector.SaveAssembly();
            Console.WriteLine();
            Console.WriteLine("Done. Press Enter to close.");
            Console.ReadLine();
        }
        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
        }
    }
}
