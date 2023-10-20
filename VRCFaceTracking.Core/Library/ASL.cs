using System.Reflection;
using System.Runtime.Loader;

namespace VRCFaceTracking.Core.Library;

public class ASL : AssemblyLoadContext
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
#elif NETSTANDARD
    protected override Assembly Load(AssemblyName assemblyName)
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
#elif NETSTANDARD
    public void UnloadAssembly() => AppDomain.Unload(appDomain);
#endif
}