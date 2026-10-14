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

    public static void Init() => Poll();

    public static void Poll()
    {
        if (dx11 != null || closed)
            return;
        if (!Anomaly.AnomalyRenderer.Found || !Anomaly.AnomalyRenderer.Decided)
            return;
        if (!Anomaly.AnomalyRenderer.AllowDx11Device)
        {
            closed = Anomaly.AnomalyRenderer.Decided;
            return;
        }

        Call("Init");
    }

    public static void Reload() => Call("Reload");

    static void Call(string method)
    {
        if (closed || !Anomaly.AnomalyRenderer.AllowDx11Device)
            return;
        if (dx11 == null)
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(SsgiDx11Host).Assembly.Location) ?? "", "Prism.SSGI.Dx11.dll");
            if (!File.Exists(path))
                return;
            dx11 = Assembly.LoadFrom(path);
        }

        dx11.GetType("ClientPlugin.SSGI.SsgiDx11Entry")?.GetMethod(method, BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
    }
}
