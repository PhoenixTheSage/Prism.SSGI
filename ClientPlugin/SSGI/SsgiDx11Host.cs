using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Prism.SSGI.Dx11")]

namespace ClientPlugin;

/// <summary>
/// Loads Prism.SSGI.Dx11 only while the process is on the DX11 renderer.
/// The main plugin assembly does not reference SharpDX or VRage.Render11.
/// </summary>
static class SsgiDx11Host
{
    static Assembly dx11;
    static bool closed;
    static bool initialized;
    static bool guestDisabled;
    static string status = "waiting for the DX11 renderer";
    public static string Status => status;

    public static void Init() => Poll();

    public static void Poll()
    {
        if (initialized || closed)
            return;
        if (!Anomaly.AnomalyRenderer.Found || !Anomaly.AnomalyRenderer.Decided)
            return;
        if (!Anomaly.AnomalyRenderer.AllowDx11Device)
        {
            closed = Anomaly.AnomalyRenderer.Decided;
            status = "DX11 companion not required on " + Anomaly.AnomalyRenderer.ActiveName;
            return;
        }
        if (!guestDisabled)
        {
            SSGI.SsgiGuest.DisableForDx11();
            guestDisabled = true;
        }
        initialized = Call("Init");
    }

    public static void Reload() => Call("Reload");

    static bool Call(string method)
    {
        if (closed || !Anomaly.AnomalyRenderer.AllowDx11Device)
            return false;
        try
        {
            if (dx11 == null)
            {
                string path = SatelliteAssembly.Find("Prism.SSGI.Dx11.dll", typeof(SsgiDx11Host).Assembly);
                if (!File.Exists(path))
                {
                    Report("DX11 companion missing: " + path);
                    return false;
                }
                dx11 = SatelliteAssembly.Load(path, typeof(SsgiDx11Host).Assembly);
                Report("DX11 companion loaded: " + path);
            }
            var entry = dx11.GetType("ClientPlugin.SSGI.SsgiDx11Entry", true);
            var call = entry.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
                ?? throw new MissingMethodException(entry.FullName, method);
            call.Invoke(null, null);
            status = "DX11 companion initialized";
            return true;
        }
        catch (Exception e)
        {
            int code = e.GetBaseException().HResult;
            if (code == unchecked((int)0x887A0005) || code == unchecked((int)0x887A0006) ||
                code == unchecked((int)0x887A0007) || code == unchecked((int)0x887A0020)) throw;
            closed = true;
            Report("DX11 " + method + " failed: " + e.GetBaseException());
            return false;
        }
    }

    static void Report(string message)
    {
        if (message == status) return;
        status = message;
        VRage.Utils.MyLog.Default.WriteLine("SSGI: " + message);
    }
}
