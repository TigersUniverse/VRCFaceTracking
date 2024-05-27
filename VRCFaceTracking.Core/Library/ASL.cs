using System.Reflection;
#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace VRCFaceTracking.Core.Library;

public class ASL 
#if !NETFRAMEWORK
: AssemblyLoadContext 
#endif
{
    private AppDomain appDomain;
    private Assembly _;
    public Assembly Assembly;

    private string filePath;
    
#if NET5_0_OR_GREATER
    protected override Assembly Load(AssemblyName assemblyName)
    {
        Assembly = LoadFromAssemblyPath(filePath);
        return Assembly;
    }
#elif NETSTANDARD || NETFRAMEWORK
    protected 
#if !NETFRAMEWORK
        override 
#endif
        Assembly Load(AssemblyName assemblyName)
    {
        appDomain = AppDomain.CreateDomain(assemblyName.Name);
        _ = Assembly.Load(assemblyName);
        Assembly = appDomain.Load(assemblyName);
        return Assembly;
    }
#endif
    
    public Assembly LoadFile(string f)
    {
        filePath = f;
        return Load(AssemblyName.GetAssemblyName(f));
    }

#if NET5_0_OR_GREATER
    public ASL(string dll, bool sumn) : base(dll, sumn){}
    public void UnloadAssembly() => Unload();
#elif NETSTANDARD || NETFRAMEWORK
    public void UnloadAssembly() => AppDomain.Unload(appDomain);
#endif
}