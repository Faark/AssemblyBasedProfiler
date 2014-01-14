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
            Console.WriteLine("Saving " + resourceFilename + " to " + targetFilename);
            var data = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceFilename);
            Console.WriteLine("Stream loaled: " + data.Length + " bytes");
            System.IO.File.WriteAllBytes(targetFilename, new System.IO.BinaryReader(data).ReadBytes((int)data.Length));
        }
        static void LoadEmbededAssembly(String resourceFilename)
        {
            var data = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceFilename);
            AppDomain.CurrentDomain.Load(new System.IO.BinaryReader(data).ReadBytes((int)data.Length));
        }
        static FileHash HashForEmbededAssembly(String resourceFilename)
        {
            return new FileHash(Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceFilename));
        }
        static int ProcessFile(System.IO.FileInfo file, ProgramArguments config)
        {
            if (!file.Exists)
            {
                Console.WriteLine("Specified file does not exist: " + file.Exists);
                Console.WriteLine();
                return 2;
            }


            var injector = new Injector(file);
            if (config.UndoProfilingByRestoringBackups)
            {
                if (injector.Check_HasModifiedMarker())
                {
                    if (System.IO.File.Exists(injector.BackupFile))
                    {
                        file.Delete();
                        System.IO.File.Move(injector.BackupFile, file.FullName);
                        Console.WriteLine("Backup restored: " + file.Name);
                        return 0;
                    }
                    else
                    {
                        Console.WriteLine("Assembly is modified but no backup exists. Skipping.");
                        return 3;
                    }
                }
                else
                {
                    Console.WriteLine("Assembly does not contain an modified marker. Skipping.");
                    return 3;
                }
            }

            if (injector.Check_HasModifiedMarker())
            {
                if (config.CheckAlreadyModified)
                {
                    Console.WriteLine("This assembly seems to already be modified for profiling. -nc can turn off this check.");
                    return 3;
                }
            }
            else
            {
                injector.Inject_ModifiedMarker();
            }

            processMethods(config, injector);

            if (config.DoAutoSaving)
            {
                injector.Inject_SetupAutoSaving(config.AutoSaveEvery, config.AutoSaveTo);
            }
            Console.WriteLine("Finalizing.");
            //injector.Inject_RegisterPrimaryMethods(cctors);

            injector.SaveAssembly(config.CreateBackup);
            if (config.PlaceOrRemoveDependencies)
            {
                Console.WriteLine("Placeing dependencies.");
                var dir = injector.File.Directory.FullName;
                SaveEmbededResourceTo("AssemblyBasedProfiller.Resources.System.Threading.dll", System.IO.Path.Combine(dir, "System.Threading.dll"));
                SaveEmbededResourceTo("AssemblyBasedProfiller.Resources.ProfilerLib.dll", System.IO.Path.Combine(dir, "ProfilerLib.dll"));
            }

            return 0;
        }
        static void ProcessDirectory(System.IO.DirectoryInfo dir, ProgramArguments config, IEnumerable<FileHash> excludedHashes)
        {
            //Console.WriteLine("Processing director: " + dir.FullName);
            foreach (var dll in dir.GetFiles("*.dll"))
            {
                if(excludedHashes.Any(excluded=>excluded.EqualsTo(dll)))
                {
                    if (config.UndoProfilingByRestoringBackups && config.PlaceOrRemoveDependencies)
                    {
                        dll.Delete();
                        Console.WriteLine("Profiling dependency removed: " + dll.Name);
                    }
                    else
                    {
                        Console.WriteLine("Excluded file skipped: " + dll.Name);
                    }
                }
                else
                {
                    ProcessFile(dll, config);
                }
            }
            if (config.PathProcessSubs)
            {
                foreach (var sub in dir.GetDirectories())
                {
                    ProcessDirectory(sub, config, excludedHashes);
                }
            }
        }
        static int Main(string[] args)
        {
            /*
            args = new[]{
                @"C:\ksp_rtPerf\GameData",
                "-perfksp"
                //@"C:\Users\Faark\Documents\GitHub\AssemblyBasedProfiler\TestApp\bin\Debug\TestApp.exe",
                //@"C:\ksp_rt\GameData\RemoteTech2\Plugins\RemoteTech2.dll",
                //@"C:\ksp_rtOld\GameData\MechJeb2\Plugins\MechJeb2.dll",
                //@"C:\Users\Faark\Documents\GitHub\AssemblyBasedProfiler\TestApp\bin\Debug\TestLib.dll",
                //"-as"
            };*/
            var arguments = ProgramArguments.ProcessArgs(args);
            if (arguments == null)
            {
                return 1;
            }

            //Safety first... lets load all for now!
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.Mdb.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.Pdb.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.Mono.Cecil.Rocks.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.System.Threading.dll");
            LoadEmbededAssembly("AssemblyBasedProfiller.Resources.ProfilerLib.dll");


            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

            if (arguments.PathIsDirectory)
            {
                var excludeHashThreading = HashForEmbededAssembly("AssemblyBasedProfiller.Resources.System.Threading.dll");
                var excludeHashProfLib = HashForEmbededAssembly("AssemblyBasedProfiller.Resources.ProfilerLib.dll");
                ProcessDirectory(new DirectoryInfo(arguments.PathToProfile), arguments, new FileHash[] { excludeHashThreading, excludeHashProfLib });
                Console.WriteLine("Done.");
                // states: -no file does match description
            }
            else
            {
                var ret = ProcessFile(new System.IO.FileInfo(arguments.PathToProfile), arguments);
                if (ret > 0)
                    return ret;
            }

            if (System.Diagnostics.Debugger.IsAttached)
            {
                Console.WriteLine();
                Console.WriteLine("Done. Press Enter to close.");
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Done.");
            }

            return 0;
        }
        static HashSet<int> usedIds = new HashSet<int>();
        static void processMethods(ProgramArguments arguments, Injector injector)
        {
            var rand = new System.Random();
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
                        // *todo: potential endless loop when ids run out or near it
                    } while (!usedIds.Add(methodId));

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
