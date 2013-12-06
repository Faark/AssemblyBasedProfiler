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
        static void SaveEmbededResourceTo(String resourceFilename, String targetFilename)
        {
            var data = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceFilename);
            System.IO.File.WriteAllBytes(targetFilename, new System.IO.BinaryReader(data).ReadBytes((int)data.Length));
        }
        static void LoadEmbededAssembly(String filename)
        {
            var data = Assembly.GetExecutingAssembly().GetManifestResourceStream(filename);
            AppDomain.CurrentDomain.Load(new System.IO.BinaryReader(data).ReadBytes((int)data.Length));
        }
        static int Main(string[] args)
        {
            args = new[]{
                @"C:\ksp_rt\GameData\RemoteTech2\Plugins\RemoteTech2.dll",
                //@"C:\ksp_rtOld\GameData\MechJeb2\Plugins\MechJeb2.dll",
                //@"C:\Users\Faark\Documents\GitHub\AssemblyBasedProfiler\TestApp\bin\Debug\TestLib.dll",
                "-as"
            };
            var arguments = ProgramArguments.ProcessArgs(args);
            if (arguments == null)
            {
                return 1;
            }

            if (!System.IO.File.Exists(arguments.AssemblyToProfile))
            {
                Console.WriteLine("Specified file does not exist: " + arguments.AssemblyToProfile);
                Console.WriteLine();
                return 2;
            }

            //Safety first... lets load all for now!
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.Mdb.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.Pdb.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.Rocks.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.System.Threading.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.ProfilerLib.dll");

            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);


            // Check whether this assembly is already manipulated. Marker might be a specific method?
            // Todo: Implement this later. Cant have enough profiling for now^^

            var injector = new Injector(arguments.AssemblyToProfile);

            processMethods(arguments, injector);

            if (arguments.DoAutoSaving)
            {
                injector.Inject_SetupAutoSaving(arguments.AutoSaveEvery, arguments.AutoSaveTo);
            }
            Console.WriteLine("Finalizing.");
            //injector.Inject_RegisterPrimaryMethods(cctors);

            injector.SaveAssembly(arguments.CreateBackup);
            if (arguments.PlaceDependencies)
            {
                Console.WriteLine("Placeing dependencies.");
                var dir = new FileInfo(arguments.AssemblyToProfile).Directory.FullName;
                SaveEmbededResourceTo("AssemblyBasedProfiller.Resources.System.Threading.dll", System.IO.Path.Combine(dir, "System.Threading.dll"));
                SaveEmbededResourceTo("AssemblyBasedProfiller.Resources.ProfilerLib.dll", System.IO.Path.Combine(dir, "ProfilerLib.dll"));
            }
            Console.WriteLine();
            Console.WriteLine("Done. Press Enter to close.");
            Console.ReadLine();


            return 0;
        }
        static void processMethods(ProgramArguments arguments, Injector injector)
        {
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
                        methodId = rand.Next(arguments.MethodIdRange_Min, arguments.MethodIdRange_Max);
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
                        injector.Inject_AddProfileCalls(methodId, arguments.VerifyLeaves, meth);
                        locals.Add(Tuple.Create(methodId, meth));
                    }
                }
                injector.Inject_RegisterMethodsAtType(locals, type);
            }
        }
        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
        }
    }
}
