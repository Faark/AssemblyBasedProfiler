AssemblyBasedProfiler - Readme
========================


AssemblyBasedProfiler is a tool that helps you collect performance data about your .NET assembly. It does so by modifying the byte code to call logging methods and was created to find performance bottlenecks in KSP mods.


How it works, what it can achieve and its limitations
-------------------------

AssemblyBasedProfiler wraps every of you methods with method calls that log whenever the method is entered or locked. It will kinda look like this:

    Profiler.Enter(method_id);
    try{
    	/* your originally method code */
    }finally{
    	Profiler.Leave();
    }
Those enter/leave events will be processed by another thread to create runtime statistics.

- ABP adds two method calls to each of your methods. This will have a significant performance impact... depending on your application easily by orders of magnitudes. This will change your apps performance characteristics. ABP tries to remove these overhead from the generated statistics, but you can't fully rely on it. An slower application might have other effects as well.
- ABP can only detect long running code segments. It won't find stuff that for example slows down rendering outside of your call tree
- It can not identify a weak scaling code segments, unless you already have already an e.g. savegame of it performing weak

Basically, all you can do is compare runtime characteristics of parts of your code compared to other code on the same run.

Usage
-------------------------

The easiest way to use ABP to debug your KSP mod is to run it with the following command line arguments:
> AssemblyBasedProfiler.exe PATH_TO_YOUR_KSP_GAMEDATA_DIRECTORY -perfksp

This pre-set will profile all your mods and write stats into a profiling.txt next to KSP.exe every 30 seconds. Once you are done, you can restore your mods into an non-profiling state from the earlier create backups via:
> AssemblyBasedProfiler.exe PATH_TO_YOUR_KSP_GAMEDATA_DIRECTORY -undoksp


There are a bunch of other parameters as well, check them out via ABP.exe /? *todo: describe them here in detail*



Feedback, problems and getting involved
-------------------------

Check out this projects GitHub-Page if you need any help, want to give feedback or might even want to contribute to the project: https://github.com/Faark/AssemblyBasedProfiler/issues

Some hints for troubleshooting:
- The modified application seems to be corrupt? Verify its IL code, for example by decompiling it with IL Spy. ABP might have produced invalid byte code. Please create an issue in such a case.

Work in progress / todo's:
- Make sure the generated IL is always valid, might switch to sth like AST?
- Reduce overhead of Profiler.Enter/.Leave. We might be able to save GCs by reusing Event-Objs, but i don't see any other significant improvements.
- Improve generated timings. It currently tries to remove the overhead via a static per call offset that is "profiled" on startup... not sure that is very great
- Save file format. It would be awesome to save it as VSP and use VS's UI to analyse it...

