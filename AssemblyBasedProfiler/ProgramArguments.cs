using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyBasedProfiller
{
    class ProgramArguments
    {
        public string PathToProfile { get; private set; }
        public bool PathIsDirectory { get; private set; }
        public bool PathProcessSubs { get; private set; }

        public bool DoAutoSaving { get; private set; }
        public int AutoSaveEvery { get; private set; }
        public string AutoSaveTo { get; private set; }

        public bool VerifyLeaves { get; private set; }
        public int MethodIdRange_Min { get; private set; }
        public int MethodIdRange_Max { get; private set; }


        public bool CreateBackup { get; private set; }
        public bool PlaceOrRemoveDependencies { get; private set; }

        public bool CheckAlreadyModified { get; private set; }
        public bool ExcludeOwnDependencies { get; private set; }

        public bool UndoProfilingByRestoringBackups { get; private set; }

        public int LogLevel { get; private set; }

        private ProgramArguments()
        {
            PathToProfile = null;
            PathIsDirectory = false;
            PathProcessSubs = false;

            DoAutoSaving = false;
            AutoSaveEvery = 30;
            AutoSaveTo = "profiling.txt";

            VerifyLeaves = false;

            MethodIdRange_Min = int.MinValue;
            MethodIdRange_Max = int.MaxValue;

            CreateBackup = true;
            PlaceOrRemoveDependencies = false;
            ExcludeOwnDependencies = true;

            CheckAlreadyModified = true;
            LogLevel = int.MaxValue;
            UndoProfilingByRestoringBackups = false;
        }
        public static void PrintHelp()
        {
            var def = new ProgramArguments();
            //Todo: Fix visuals once args are finalized.
            //Console.WriteLine - Line length last char marker                                                X
            Console.WriteLine("Manipulates an .NET assembly in a way that allows it to record profiling data.");
            Console.WriteLine();
            Console.WriteLine("Usage: AssemblyBasedProfiller assembly [-as] [-ase seconds] [-ast file] [-undo]");
            Console.WriteLine("                                       [-nb] [-dep] [-v] [-r min max] [-log lvl]");
            Console.WriteLine("Alternative usages:");
            Console.WriteLine("       AssemblyBasedProfiller directory -dir | -all [options]");
            Console.WriteLine("       AssemblyBasedProfiller gamedata -perfksp | -undoksp [options]");
            Console.WriteLine();
            Console.WriteLine("Details:");
            Console.WriteLine("   assembly         The .NET assembly that will be modified.");
            Console.WriteLine("   directory        The directory to search for assemblies. Use -dir for the");
            Console.WriteLine("                    current and -all to include subdirectories.");
            Console.Write("   gamedata         The KSP gamedata directory. Use it with -perfksp run pre-set");
            Console.Write("                    options to profile all installed ksp mods. Undo is a pre-set");
            Console.WriteLine("                    to remove changes made by perfksp.");
            Console.WriteLine("Options:");
            Console.Write("   -as              Turns on automatic report saving using a default config. Use");
            Console.WriteLine("                    -ase and -ast to configure autosave behavior.");
            Console.WriteLine("   -ase seconds     Minimal time between automatic saves. It only happens when ");
            Console.WriteLine("                    idle, though! Implies -as. Default: "+def.AutoSaveEvery);
            Console.WriteLine("   -ast file        The file to write the automatic report into.");
            Console.WriteLine("                    Implies -as. Default: " + def.AutoSaveTo);
            Console.Write("   -nb              Don't creating a .backup of the assembly before altering it.");
            Console.WriteLine("   -nc              No checking whether this assembly is already modified.");
            Console.WriteLine("   -dep             If given, ABP will place neccessary profiling-dependencies");
            Console.WriteLine("                    next to the assembly.");
            Console.WriteLine("   -v               Turns on Leave verification. Helps to find errors in the");
            Console.WriteLine("                    modded assembly. See documentation.");
            Console.WriteLine("   -r min max       Specifies the ID range for methods. Useful to prevent");
            Console.WriteLine("                    confilicts while profiling multpile assemblies.");
            Console.WriteLine("   -log level       Todo: Write info; Todo: implement");
            Console.WriteLine("   -undo            Tries to remove all profiling stuff by restoring backups. Todo: write info");

            Console.WriteLine();
            Console.WriteLine();
        }
        static bool getNextInt(Queue<string> args, out int num)
        {
            if (args.Count > 0)
            {
                var s = args.Dequeue();
                int result;
                if (int.TryParse(s, out result))
                {
                    num = result;
                    return true;
                }
            }
            num = 0;
            return false;
        }
        bool processArg(string arg, Queue<string> argQueue)
        {
            switch (arg.ToUpper())
            {
                case "-?":
                case "/?":
                case "-HELP":
                case "/HELP":
                    PrintHelp();
                    return true;
                case "-AS":
                case "/AS":
                    DoAutoSaving = true;
                    break;
                case "-ASE":
                case "/ASE":
                    int seconds;
                    if (getNextInt(argQueue, out seconds) && seconds > 0)
                    {
                        DoAutoSaving = true;
                        AutoSaveEvery = seconds;
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments: ASE value is not valid! Expected");
                        Console.WriteLine("     -ase int");
                        Console.WriteLine("  where int > 0.");
                        return true;
                    }
                    break;
                case "-AST":
                case "/AST":
                    if (argQueue.Count > 0)
                    {
                        DoAutoSaving = true;
                        AutoSaveTo = argQueue.Dequeue();
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments: AST target file name is missing!");
                        return true;
                    }
                    break;
                case "-NB":
                case "/NB":
                    CreateBackup = false;
                    break;
                case "-NC":
                case "/NC":
                    CheckAlreadyModified = false;
                    break;
                case "-DEP":
                case "/DEP":
                    PlaceOrRemoveDependencies = true;
                    break;
                case "-V":
                case "/V":
                    VerifyLeaves = true;
                    break;
                case "-R":
                case "/R":
                    int min, max;
                    if (getNextInt(argQueue, out min) && getNextInt(argQueue, out max))
                    {
                        MethodIdRange_Min = Math.Min(min, max);
                        MethodIdRange_Max = Math.Max(min, max);
                    }
                    else
                    {
                        Console.WriteLine("Invalid arguments: R values are not valid! Expected");
                        Console.WriteLine("     -r min max");
                        Console.WriteLine("  where min and max are integers.");
                    }
                    break;
                case "-DIR":
                case "/DIR":
                    PathIsDirectory = true;
                    PathProcessSubs = false;
                    break;
                case "-ALL":
                case "/ALL":
                    PathIsDirectory = true;
                    PathProcessSubs = true;
                    break;
                case "-PERFKSP":
                case "/PERFKSP":
                    PathIsDirectory = true;
                    PathProcessSubs = true;
                    DoAutoSaving = true;
                    PlaceOrRemoveDependencies = true;
                    CreateBackup = true;
                    CheckAlreadyModified = true;
                    VerifyLeaves = false;
                    break;
                case "-UNDO":
                case "/UNDO":
                    UndoProfilingByRestoringBackups = true;
                    break;
                case "-UNDOKSP":
                case "/UNDOKSP":
                    PathIsDirectory = true;
                    PathProcessSubs = true;
                    PlaceOrRemoveDependencies = true;
                    UndoProfilingByRestoringBackups = true;
                    break;
                default:
                    throw new NotImplementedException("Todo: Handle unknown/invalid cmds.");
            }
            return false;
        }
        public static ProgramArguments ProcessArgs(string[] args)
        {
            var argQueue = new Queue<String>(args);
            if ((argQueue.Count <= 0) || new[] { "-?", "/?", "-HELP", "/HELP" }.Contains(argQueue.Peek().ToUpper()))
            {
                PrintHelp();
                return null;
            }
            var newArgs = new ProgramArguments();
            newArgs.PathToProfile = argQueue.Dequeue();
            while (argQueue.Count > 0)
            {
                if (newArgs.processArg(argQueue.Dequeue(), argQueue))
                    return null;
            }
            return newArgs;
        }
    }
}
