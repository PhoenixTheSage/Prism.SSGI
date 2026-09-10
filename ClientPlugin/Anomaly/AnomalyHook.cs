using System;
using System.Reflection;
using VRage.Render11.RenderContext;
using VRage.Render11.Resources;
using VRage.Utils;

namespace ClientPlugin.Anomaly;

/// <summary>
/// Runtime bind to Anomaly Shader Framework. Resolve well-known types by
/// name — no compile-time reference.
/// </summary>
internal static class AnomalyHook
{
    public const string PackRegistryTypeName = "ClientPlugin.Shaders.ShaderPackRegistry";
    public const string FullscreenTypeName = "ClientPlugin.Shaders.FullscreenPassRegistry";
    public const string OwnedPassTypeName = "ClientPlugin.Shaders.OwnedPassRegistry";
    public const string CatalogTypeName = "ClientPlugin.Buffers.BufferCatalog";
    public const string PublishedBufferTypeName = "ClientPlugin.Buffers.PublishedBuffer";
    public const string PackId = "prism.ssgi";
    public const string TraceProgramId = "prism.ssgi.trace";
    public const string MipsPassId = "prism.ssgi.uniforms";
    public const string SvgfPassId = "prism.ssgi.svgf";
    public const string TraceOutputName = "pass.prism.ssgi.trace";
    public const string LitMipsName = "litMips";
    public const string HistoryName = "ssgi.history";
    public const string PrevDepthName = "ssgi.prevDepth";

    public const int TemporalInColor = 1;
    public const int PhaseBeforeFullscreen = 0;
    public const int PhaseAfterFullscreen = 1;

    static readonly object Gate = new();

    static MethodInfo _registerPack;
    static MethodInfo _setUniforms;
    static MethodInfo _requestSrv;
    static MethodInfo _setEnabled;
    static MethodInfo _requestLitMips;
    static MethodInfo _registerOwned;
    static MethodInfo _catalogActive;
    static MethodInfo _catalogPublish;
    static MethodInfo _registerLifetime;
    static Type _publishedBufferType;
    static MethodInfo _publishedPublish;
    static bool _packRegistered;
    static bool _probed;
    static bool _lifetimeRegistered;

    public static bool RegistryFound
    {
        get
        {
            Probe();
            lock (Gate)
                return _registerPack != null;
        }
    }

    public static bool PackRegistered
    {
        get { lock (Gate) return _packRegistered; }
    }

    public static void Probe()
    {
        lock (Gate)
        {
            if (_probed && _registerPack != null)
                return;
            _probed = true;
            ScanUnlocked();
        }
    }

    public static bool TryRegisterPack(string root)
    {
        if (string.IsNullOrEmpty(root))
            return false;
        Probe();
        MethodInfo register;
        lock (Gate)
            register = _registerPack;
        if (register == null)
        {
            MyLog.Default.WriteLine("SSGI: Anomaly ShaderPackRegistry not found");
            return false;
        }

        try
        {
            register.Invoke(null, new object[] { PackId, root });
            lock (Gate)
                _packRegistered = true;
            MyLog.Default.WriteLine("SSGI: registered Anomaly pack " + PackId + " at " + root);
            return true;
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: ShaderPackRegistry.Register failed: " + e.Message);
            return false;
        }
    }

    public static bool SetUniforms(string programId, float[] values)
    {
        Probe();
        MethodInfo set;
        lock (Gate)
            set = _setUniforms;
        if (set == null || values == null)
            return false;
        try
        {
            return set.Invoke(null, new object[] { programId, values }) is not false;
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: SetUniforms failed: " + e.Message);
            return false;
        }
    }

    public static bool RequestSrv(string programId, string catalogName, int slot = -1)
    {
        Probe();
        MethodInfo request;
        lock (Gate)
            request = _requestSrv;
        if (request == null)
            return false;
        try
        {
            var args = request.GetParameters().Length == 3
                ? new object[] { programId, catalogName, slot }
                : new object[] { programId, catalogName };
            return request.Invoke(null, args) is not false;
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: RequestSrv failed: " + e.Message);
            return false;
        }
    }

    public static bool SetEnabled(string programId, bool enabled)
    {
        Probe();
        MethodInfo set;
        lock (Gate)
            set = _setEnabled;
        if (set == null)
            return false;
        try
        {
            return set.Invoke(null, new object[] { programId, enabled }) is not false;
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: SetEnabled failed: " + e.Message);
            return false;
        }
    }

    public static void RequestLitMips(int mipLevels = 5)
    {
        Probe();
        MethodInfo request;
        lock (Gate)
            request = _requestLitMips;
        if (request == null)
            return;
        try
        {
            request.Invoke(null, new object[] { mipLevels });
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: RequestLitMips failed: " + e.Message);
        }
    }

    public static bool TryRegisterOwnedPass(string id, string slot, int priority, int temporalPolicy,
        Action<object> draw, int phase)
    {
        Probe();
        MethodInfo register;
        lock (Gate)
            register = _registerOwned;
        if (register == null || draw == null)
            return false;
        try
        {
            var parameters = register.GetParameters();
            if (parameters.Length >= 6)
                register.Invoke(null, new object[] { id, slot, priority, temporalPolicy, draw, phase });
            else
                register.Invoke(null, new object[] { id, slot, priority, temporalPolicy, draw });
            return true;
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: OwnedPassRegistry.Register failed: " + e.Message);
            return false;
        }
    }

    public static void RegisterLifetime(Action onResolutionChanged, Action onDeviceEnd)
    {
        Probe();
        MethodInfo register;
        lock (Gate)
        {
            register = _registerLifetime;
            if (_lifetimeRegistered || register == null)
                return;
            _lifetimeRegistered = true;
        }

        try
        {
            register.Invoke(null, new object[] { PackId, onResolutionChanged, onDeviceEnd });
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: RegisterLifetime failed: " + e.Message);
        }
    }

    public static ISrvBindable TryGetSrv(string catalogName)
    {
        var buffer = TryGetBuffer(catalogName);
        if (buffer == null)
            return null;
        try
        {
            // Catalog entries are different runtime types (PublishedBuffer,
            // CatalogTexture, VelocitySharedBuffer). A PropertyInfo cached
            // from the first type throws TargetException on the others;
            // the catch returned null and SVGF blended zeros onto LBuffer
            // while Trace still ran.
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var type = buffer.GetType();
            if (type.GetProperty("IsAvailable", flags)?.GetValue(buffer) is not true)
                return null;
            return type.GetProperty("Srv", flags)?.GetValue(buffer) as ISrvBindable;
        }
        catch
        {
            return null;
        }
    }

    public static bool TryPublish(string name, ISrvBindable srv, int width, int height)
    {
        if (srv == null || width <= 0 || height <= 0)
            return false;
        Probe();
        MethodInfo publish;
        Type publishedType;
        MethodInfo publishedPublish;
        lock (Gate)
        {
            publish = _catalogPublish;
            publishedType = _publishedBufferType;
            publishedPublish = _publishedPublish;
        }

        if (publish == null || publishedType == null || publishedPublish == null)
            return false;

        try
        {
            var native = NativePointer(srv);
            var instance = Activator.CreateInstance(publishedType);
            publishedPublish.Invoke(instance, new object[] { srv, native, width, height });
            return publish.Invoke(null, new object[] { PackId, name, instance }) is not false;
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: BufferCatalog.Publish failed: " + e.Message);
            return false;
        }
    }

    public static object TryCreatePublishedBuffer()
    {
        Probe();
        Type type;
        lock (Gate)
            type = _publishedBufferType;
        if (type == null)
            return null;
        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    public static bool TryPublishExisting(object publishedBuffer, string name, ISrvBindable srv, int width, int height)
    {
        if (publishedBuffer == null || srv == null)
            return false;
        Probe();
        MethodInfo publish;
        MethodInfo publishedPublish;
        lock (Gate)
        {
            publish = _catalogPublish;
            publishedPublish = _publishedPublish;
        }

        if (publish == null || publishedPublish == null)
            return false;
        try
        {
            publishedPublish.Invoke(publishedBuffer, new object[] { srv, NativePointer(srv), width, height });
            return publish.Invoke(null, new object[] { PackId, name, publishedBuffer }) is not false;
        }
        catch (Exception e)
        {
            MyLog.Default.WriteLine("SSGI: publish existing failed: " + e.Message);
            return false;
        }
    }

    public static MyRenderContext GetRenderContext(object ctx)
    {
        if (ctx is MyRenderContext rc)
            return rc;
        try
        {
            var prop = ctx?.GetType().GetProperty("Rc");
            if (prop?.GetValue(ctx) is MyRenderContext fromCtx)
                return fromCtx;
        }
        catch
        {
            // fall through
        }

        // AfterLighting records on Keen's transparent deferred worker.
        // MyRender11.RC is the immediate context — using it from that
        // worker races the render thread and hangs the device.
        return null;
    }

    static object TryGetBuffer(string catalogName)
    {
        Probe();
        MethodInfo active;
        lock (Gate)
            active = _catalogActive;
        if (active == null || string.IsNullOrWhiteSpace(catalogName))
            return null;
        try
        {
            return active.Invoke(null, new object[] { catalogName });
        }
        catch
        {
            return null;
        }
    }

    static IntPtr NativePointer(ISrvBindable srv)
    {
        try
        {
            var resource = srv.Resource;
            return resource != null ? resource.NativePointer : IntPtr.Zero;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    static void ScanUnlocked()
    {
        Assembly[] assemblies;
        try
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        catch
        {
            return;
        }

        foreach (var assembly in assemblies)
        {
            if (assembly == null || assembly == typeof(AnomalyHook).Assembly)
                continue;
            try
            {
                var pack = assembly.GetType(PackRegistryTypeName, throwOnError: false, ignoreCase: false);
                var fullscreen = assembly.GetType(FullscreenTypeName, throwOnError: false, ignoreCase: false);
                var owned = assembly.GetType(OwnedPassTypeName, throwOnError: false, ignoreCase: false);
                var catalog = assembly.GetType(CatalogTypeName, throwOnError: false, ignoreCase: false);
                var published = assembly.GetType(PublishedBufferTypeName, throwOnError: false, ignoreCase: false);

                if (pack != null && _registerPack == null)
                    _registerPack = pack.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);

                if (fullscreen != null)
                {
                    _setUniforms ??= fullscreen.GetMethod("SetUniforms", BindingFlags.Public | BindingFlags.Static);
                    _requestSrv ??= FindStatic(fullscreen, "RequestSrv");
                    _setEnabled ??= FindStatic(fullscreen, "SetEnabled", typeof(string), typeof(bool));
                    _requestLitMips ??= FindStatic(fullscreen, "RequestLitMips");
                }

                if (owned != null && _registerOwned == null)
                    _registerOwned = FindOwnedRegister(owned);

                if (catalog != null)
                {
                    _catalogActive ??= FindStatic(catalog, "Active", typeof(string));
                    _catalogPublish ??= FindStatic(catalog, "Publish", typeof(string), typeof(string), null);
                    _registerLifetime ??= FindStatic(catalog, "RegisterLifetime");
                }

                if (published != null && _publishedBufferType == null)
                {
                    _publishedBufferType = published;
                    _publishedPublish = published.GetMethod("Publish", BindingFlags.Public | BindingFlags.Instance);
                }

                if (_registerPack != null && _setUniforms != null && _registerOwned != null && _catalogActive != null)
                    return;
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("SSGI: Anomaly scan skipped: " + e.GetType().Name);
            }
        }
    }

    static MethodInfo FindOwnedRegister(Type type)
    {
        MethodInfo fallback = null;
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (method.Name != "Register")
                continue;
            var parameters = method.GetParameters();
            if (parameters.Length < 5 || parameters[0].ParameterType != typeof(string))
                continue;
            if (parameters.Length >= 6 && parameters[5].ParameterType == typeof(int))
                return method;
            fallback ??= method;
        }

        return fallback;
    }

    static MethodInfo FindStatic(Type type, string name, params Type[] parameters)
    {
        if (type == null)
            return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        if (parameters == null || parameters.Length == 0)
        {
            foreach (var method in type.GetMethods(flags))
            {
                if (method.Name == name)
                    return method;
            }

            return null;
        }

        foreach (var method in type.GetMethods(flags))
        {
            if (method.Name != name)
                continue;
            var methodParams = method.GetParameters();
            if (methodParams.Length != parameters.Length)
                continue;
            var match = true;
            for (var i = 0; i < parameters.Length; i++)
            {
                if (parameters[i] != null && methodParams[i].ParameterType != parameters[i])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return method;
        }

        return null;
    }
}
