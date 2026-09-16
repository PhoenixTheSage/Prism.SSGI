using System.Globalization;
using System.Text;
using ClientPlugin.Anomaly;
using ClientPlugin.Config;
using ClientPlugin.Gui;
using Sandbox.Graphics.GUI;
using VRageRender;

namespace ClientPlugin.SSGI;

public static class SSGIStatus
{
    public static string CurrentText
    {
        get
        {
            var sb = new StringBuilder();
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
        sb.Append(" · mip ").Append(cfg.InputMipLevel);
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
        var size = SSGIPass.PassResolution;
        var scene = MyRender11.ResolutionI;
        sb.Append("Pass     ").Append(size.X).Append('x').Append(size.Y);
        sb.Append(" · scene ").Append(scene.X).Append('x').Append(scene.Y);
        sb.Append(" · ready ").Append(Yes(SSGIPass.Ready));
        sb.Append(" · before ").Append(SSGIPass.BeforeFrames);
        sb.Append(" · after ").AppendLine(SSGIPass.AfterFrames.ToString(CultureInfo.InvariantCulture));
        sb.Append("Denoiser ").AppendLine(SSGIPass.DenoiserCompileError ? "compile failed" : "ok");
        var denoiseErr = SSGIPass.DenoiserError;
        if (!string.IsNullOrEmpty(denoiseErr))
            sb.Append("     ").AppendLine(denoiseErr);
        sb.Append("TraceSRV ").Append(SSGIPass.LastTraceSrv ? "yes" : "missing");
        sb.Append(" · skip ").AppendLine(SSGIPass.LastSkip);
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
