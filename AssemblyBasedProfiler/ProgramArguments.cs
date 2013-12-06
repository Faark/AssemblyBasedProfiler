using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssemblyBasedProfiller
{
    class ProgramArguments
    {
        public string AssemblyToProfile { get; private set; }

        public bool DoAutoSaving { get; private set; }
        public int AutoSaveEvery { get; private set; }
        public string AutoSaveTo { get; private set; }

        public bool VerifyLeaves { get; private set; }
        public int MethodIdRange_Min { get; private set; }
        public int MethodIdRange_Max { get; private set; }

        public bool CreateBackup { get; private set; }
        public bool PlaceDependencies { get; private set; }

        private ProgramArguments()
        {
            DoAutoSaving = false;
            AutoSaveEvery = 30;
            AutoSaveTo = "profiling.txt";

            VerifyLeaves = false;

            MethodIdRange_Min = int.MinValue;
            MethodIdRange_Max = int.MaxValue;

            CreateBackup = true;
            PlaceDependencies = false;
        }
        public static void PrintHelp()
        {
            var def = new ProgramArguments();
            //Console.WriteLine - Line length last char marker                                                X
            Console.WriteLine("Manipulates an .NET assembly in a way that allows it to record profiling data.");
            Console.WriteLine();
            Console.WriteLine("Usage: AssemblyBasedProfiller assembly [-as] [-ase seconds] [-ast file]");
            Console.WriteLine("                                       [-nb] [-dep] [-v] [-r min max]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("   assembly         The .NET assembly that will be modified.");
            Console.WriteLine("   -as              Turns on automatic report saving. Configure it via -ase and -ast.");
            Console.WriteLine("   -ase seconds     Minimal time between automatic saves. It only happens when idle, though! Implies as. Def: "+def.AutoSaveEvery);
            Console.WriteLine("   -ast file        The file to write the automatic report into. Implies as. Def: "+def.AutoSaveTo);
            Console.WriteLine("   -nb              Turns off creating a .backup of the assembly before altering it.");
            Console.WriteLine("   -dep             If given, ABP will place neccessary profiling-dependencies next to the assembly.");
            Console.WriteLine("   -v               Turns on Leave verification. Helps to find errors in the modded assembly. See docs.");
            Console.WriteLine("   -r min max       Specifies the ID range for methods. Useful to prevent confilicts while profiling multpile assemblies.");
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
        public static ProgramArguments ProcessArgs(string[] args)
        {
            var argQueue = new Queue<String>(args);
            if (argQueue.Count <= 0 || new[] { "-?", "/?", "-HELP", "/HELP" }.Contains(argQueue.Peek().ToUpper()))
            {
                PrintHelp();
                return null;
            }
            var newArgs = new ProgramArguments();
            newArgs.AssemblyToProfile = argQueue.Dequeue();
            while (argQueue.Count > 0)
            {
                switch (argQueue.Dequeue().ToUpper())
                {
                    case "-?":
                    case "/?":
                    case "-HELP":
                    case "/HELP":
                        PrintHelp();
                        return null;
                    case "-AS":
                    case "/AS":
                        newArgs.DoAutoSaving = true;
                        break;
                    case "-ASE":
                    case "/ASE":
                        int seconds;
                        if (getNextInt(argQueue, out seconds) && seconds > 0)
                        {
                            newArgs.DoAutoSaving = true;
                            newArgs.AutoSaveEvery = seconds;
                        }
                        else
                        {
                            Console.WriteLine("Invalid arguments: ASE value is not valid! Expected");
                            Console.WriteLine("     -ase int");
                            Console.WriteLine("  where int > 0.");
                            return null;
                        }
                        break;
                    case "-AST":
                    case "/AST":
                        if (argQueue.Count > 0)
                        {
                            newArgs.DoAutoSaving = true;
                            newArgs.AutoSaveTo = argQueue.Dequeue();
                        }
                        else
                        {
                            Console.WriteLine("Invalid arguments: AST target file name is missing!");
                            return null;
                        }
                        break;
                    case "-NB":
                    case "/NB":
                        newArgs.CreateBackup = false;
                        break;
                    case "-DEP":
                    case "/DEP":
                        newArgs.PlaceDependencies = true;
                        break;
                    case "-V":
                    case "/V":
                        newArgs.VerifyLeaves = true;
                        break;
                    case "-R":
                    case "/R":
                        int min, max;
                        if (getNextInt(argQueue, out min) && getNextInt(argQueue, out max))
                        {
                            newArgs.MethodIdRange_Min = Math.Min(min, max);
                            newArgs.MethodIdRange_Max = Math.Max(min, max);
                        }
                        else
                        {
                            Console.WriteLine("Invalid arguments: R values are not valid! Expected");
                            Console.WriteLine("     -r min max");
                            Console.WriteLine("  where min and max are integers.");
                        }
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            return newArgs;
        }
    }
}
