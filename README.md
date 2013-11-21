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