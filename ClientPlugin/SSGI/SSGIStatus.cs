using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using ClientPlugin.Anomaly;
using ClientPlugin.Config;
using ClientPlugin.Gui;
using Sandbox.Graphics.GUI;

namespace ClientPlugin.SSGI;

public static class SSGIStatus
{
    public static string CurrentText
    {
        get
        {
            var sb = new StringBuilder();
            sb.AppendLine(PluginStatusPage.RendererLine);
            AppendConfig(sb);
            sb.AppendLine();
            AppendAnomaly(sb);
            sb.AppendLine();
            AppendRuntime(sb);
            sb.AppendLine();
            AppendPaths(sb);
            return sb.ToString();
        }
    }

    public static void Show()
    {
        MyGuiSandbox.AddScreen(new StatusScreen(CurrentText));
    }

    static void AppendConfig(StringBuilder sb)
    {
        var cfg = Plugin.SSGIConfig;
        if (cfg == null)
        {
            sb.AppendLine("SSGI  config not loaded");
            return;
        }

        var preset = cfg.DetectPreset();
        sb.Append("SSGI  ").Append(cfg.Enabled ? "on" : "off");
        sb.Append(" · ").Append(preset);
        sb.Append(" · scale ").Append(cfg.TraceScale().ToString("0.##", CultureInfo.InvariantCulture));
        sb.Append(" · slices ").Append(cfg.SliceCount);
        sb.Append(" · steps ").Append(cfg.StepCount);
        sb.Append(" · à-trous ").AppendLine(cfg.DenoiserBlurIterations.ToString(CultureInfo.InvariantCulture));
        sb.Append("     intensity ").Append(cfg.GIIntensity.ToString("0.##", CultureInfo.InvariantCulture));
        sb.Append(" · radius ").Append(cfg.Radius.ToString("0.##", CultureInfo.InvariantCulture));
        sb.Append(" · mip 0");
        sb.Append(" · history ").AppendLine(cfg.DenoiserMaxHistory.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendAnomaly(StringBuilder sb)
    {
        sb.Append("Anomaly  registry ").Append(Yes(AnomalyHook.RegistryFound));
        sb.Append(" · pack ").AppendLine(Yes(AnomalyHook.PackRegistered));
        if (AnomalyHook.TryGetProgramStatus(AnomalyHook.TraceProgramId, out var trace))
            sb.Append("Trace    ").AppendLine(trace);
        else
            sb.AppendLine("Trace    unknown (Anomaly TryGetProgramStatus missing or id not registered)");
    }

    static void AppendRuntime(StringBuilder sb)
    {
        if (!AnomalyRenderer.AllowDx11Device)
        {
            sb.AppendLine(AnomalyRenderer.AllowGuestWork
                ? "Runtime  Vulkan through Anomaly fullscreen programs"
                : "Runtime  waiting for an operational Anomaly renderer");
            foreach (var id in new[] { "prism.ssgi.temporal", "prism.ssgi.moments", "prism.ssgi.atrous0", "prism.ssgi.composite" })
                sb.Append(id).Append("  ").AppendLine(PluginStatusPage.ProgramStatus(id));
            return;
        }
        var pass = Find("ClientPlugin.SSGI.SSGIPass");
        if (pass == null)
        {
            sb.AppendLine("Runtime  DX11 companion pass unavailable (not loaded or initialization failed)");
            return;
        }
        int px = ReadInt(pass, "PassResolution", "X");
        int py = ReadInt(pass, "PassResolution", "Y");
        var scene = Find("ClientPlugin.Backend.SceneView");
        int sx = Convert.ToInt32(scene?.GetProperty("Width")?.GetValue(null) ?? 0);
        int sy = Convert.ToInt32(scene?.GetProperty("Height")?.GetValue(null) ?? 0);
        sb.Append("Pass     ").Append(px).Append('x').Append(py);
        sb.Append(" · scene ").Append(sx).Append('x').Append(sy);
        sb.Append(" · ready ").Append(Yes(Flag(pass, "Ready")));
        sb.Append(" · before ").Append(Convert.ToInt32(pass?.GetProperty("BeforeFrames")?.GetValue(null) ?? 0));
        sb.Append(" · after ").AppendLine(Convert.ToInt32(pass?.GetProperty("AfterFrames")?.GetValue(null) ?? 0).ToString(CultureInfo.InvariantCulture));
        sb.Append("Denoiser ").AppendLine(Flag(pass, "DenoiserCompileError") ? "compile failed" : "ok");
        var denoiseErr = pass?.GetProperty("DenoiserError")?.GetValue(null) as string;
        if (!string.IsNullOrEmpty(denoiseErr))
            sb.Append("     ").AppendLine(denoiseErr);
        sb.Append("TraceSRV ").Append(Flag(pass, "LastTraceSrv") ? "yes" : "missing");
        sb.Append(" · skip ").AppendLine(pass?.GetProperty("LastSkip")?.GetValue(null) as string ?? "");
    }

    static Type Find(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type;
            try { type = assembly.GetType(name, false); }
            catch { continue; }
            if (type != null)
                return type;
        }

        return null;
    }

    static bool Flag(Type type, string property) => type?.GetProperty(property)?.GetValue(null) is true;

    static int ReadInt(Type type, string property, string field)
    {
        var value = type?.GetProperty(property)?.GetValue(null);
        var inner = value?.GetType().GetField(field)?.GetValue(value) ?? value?.GetType().GetProperty(field)?.GetValue(value);
        return inner == null ? 0 : Convert.ToInt32(inner);
    }

    static void AppendPaths(StringBuilder sb)
    {
        sb.AppendLine("Paths");
        sb.Append("     pack    ").AppendLine(Empty(Plugin.AnomalyPackRoot));
        sb.Append("     shaders ").AppendLine(Empty(Plugin.ShaderDirectory));
    }

    static string Yes(bool value) => value ? "yes" : "no";

    static string Empty(string value) => string.IsNullOrEmpty(value) ? "(none)" : value;
}
