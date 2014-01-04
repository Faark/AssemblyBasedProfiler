This doc is Wip/Todo!




AssemblyBasedProfiler is a way to collect performance data about your .NET assembly. It does so by modifying it's byte code to call logging methods. It was created to find performance bottlenecks in KSP mods.

- How it works, what it can do & what it cannot

Profiling isn't easy and measuring it 
Some notes.

-- profiling does change the performance. Collecting performance data in a way like ABP does even chnage your performance characteristics in a numbre of ways. Since every method call is logged, every method call has a significant overhead. 

- How to use it

Command line arguments

-as for autosave. You you can also use ... and/or ... for ...


Did it work?
- what does console output mean
- verify via IL decompiler

- Getting Involved


I needed to profile code within an embedded .net3.5 runtime that doesn't allow me to do anything but to run my assembly. So all this "profiler" basically does is wrapping every of your methods in an

Profiler.Enter(method_id);
try{
	/* your originally method code */
}finally{
	Profiler.Leave();
}


Work in progress / todo's:
- Make sure the generated IL is always valid, might switch to sth like AST?
- Reduce overhead of Profiler.Enter/.Leave. We might be able to save GCs by reusing Event-Objs, but i don't see any other significant improvements.
- Improve generated timings. I currently try to remove overhead via a static per call offset that is "profiled" on startup... not sure that is very great
- Save file format. It would be awesome to save it as VSP and use VS's UI to analyse it...