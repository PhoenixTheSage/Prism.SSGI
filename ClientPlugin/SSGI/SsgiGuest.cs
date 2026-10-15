using System;
using System.Reflection;
using ClientPlugin.Anomaly;
using ClientPlugin.Config;

namespace ClientPlugin.SSGI;

/// <summary>
/// Vulkan SVGF. The trace stays PublishOnly. Temporal, moments, and à-trous
/// keep their own history, then DirectAdd the filtered color into world HDR.
/// </summary>
static class SsgiGuest
{
    const string TemporalId = "prism.ssgi.temporal";
    const string MomentsId = "prism.ssgi.moments";
    const string CompositeId = "prism.ssgi.composite";
    const string PrevDepthId = "prism.ssgi.prevDepth";
    const string PrevNormalId = "prism.ssgi.prevNormal";
    static readonly string[] AtrousIds =
    {
        "prism.ssgi.atrous0", "prism.ssgi.atrous1", "prism.ssgi.atrous2", "prism.ssgi.atrous3",
    };

    static bool announced;
    static readonly Random Rand = new Random();

    public static void DisableForDx11()
    {
        // DX11 owns SVGF in C#. The json guest chain defaults on and shares
        // history catalog names; running both would overwrite native history.
        foreach (var id in new[] { TemporalId, MomentsId, CompositeId, PrevDepthId, PrevNormalId })
            AnomalyHook.SetEnabled(id, false);
        foreach (var id in AtrousIds)
            AnomalyHook.SetEnabled(id, false);
    }

    public static void Tick()
    {
        var config = ClientPlugin.Plugin.SSGIConfig;
        if (config == null)
            return;
        if (!announced)
        {
            announced = true;
            VRage.Utils.MyLog.Default.WriteLine("SSGI: Vulkan SVGF runs on the guest bus and adds into world HDR.");
        }

        float far = Float(Frame(), "FarClip");
        if (far < 1f)
            far = 1000000f;
        bool on = config.Enabled;
        float scale = config.ResolutionScale > 0f ? config.ResolutionScale : 0.5f;
        int iterations = config.DenoiserBlurIterations < 1 ? 1 : config.DenoiserBlurIterations > 4 ? 4 : config.DenoiserBlurIterations;
        string source = TemporalId;
        AnomalyHook.RequestSrv(AnomalyHook.TraceProgramId, "hiZ", 8);
        AnomalyHook.SetScale(AnomalyHook.TraceProgramId, scale);
        AnomalyHook.SetEnabled(AnomalyHook.TraceProgramId, on);
        AnomalyHook.SetUniforms(AnomalyHook.TraceProgramId, Uniforms(config, far));
        BindHistory(TemporalId, "pass.prism.ssgi.trace");
        BindHistory(MomentsId, "pass.prism.ssgi.trace");
        AnomalyHook.SetScale(TemporalId, scale);
        AnomalyHook.SetScale(MomentsId, scale);
        AnomalyHook.SetEnabled(TemporalId, on);
        AnomalyHook.SetEnabled(MomentsId, on);
        var filter = new float[4];
        filter[0] = 32f;
        filter[1] = 1f;
        AnomalyHook.SetUniforms(TemporalId, filter);
        AnomalyHook.SetUniforms(MomentsId, filter);
        for (int i = 0; i < AtrousIds.Length; i++)
        {
            bool enabled = on && i < iterations;
            int shift = iterations - 1 - i;
            int step = shift >= 0 ? 1 << shift : 1;
            AnomalyHook.SetScale(AtrousIds[i], scale);
            AnomalyHook.SetEnabled(AtrousIds[i], enabled);
            AnomalyHook.RequestSrv(AtrousIds[i], "pass." + source, 7);
            var stepUniform = new float[4];
            stepUniform[0] = step;
            AnomalyHook.SetUniforms(AtrousIds[i], stepUniform);
            if (enabled)
                source = AtrousIds[i];
        }
        AnomalyHook.RequestSrv(CompositeId, "pass." + source, 7);
        AnomalyHook.SetEnabled(CompositeId, on);
        AnomalyHook.SetScale(PrevDepthId, scale);
        AnomalyHook.SetScale(PrevNormalId, scale);
        AnomalyHook.SetEnabled(PrevDepthId, on);
        AnomalyHook.SetEnabled(PrevNormalId, on);
    }

    static void BindHistory(string program, string trace)
    {
        AnomalyHook.RequestSrv(program, "ssgi.colorHistory", 7);
        AnomalyHook.RequestSrv(program, trace, 8);
        AnomalyHook.RequestSrv(program, "ssgi.prevDepth", 9);
        AnomalyHook.RequestSrv(program, "ssgi.prevNormal", 12);
        AnomalyHook.RequestSrv(program, "ssgi.momentsHistory", 13);
    }

    static float[] Uniforms(SSGIConfig config, float far)
    {
        var frame = Frame();
        float width = Float(frame, "RenderWidth");
        float height = Float(frame, "RenderHeight");
        float proj11 = Scale(frame, 0);
        float proj22 = Scale(frame, 1);
        var values = new float[24];
        values[0] = height * proj11 * 0.25f;
        values[1] = config.Enabled ? Clamp(config.GIIntensity * 2f) : 0f;
        values[2] = 1.5f;
        values[3] = Clamp(config.SliceCount);
        values[4] = Clamp(config.StepCount);
        values[5] = Clamp(config.Radius);
        values[6] = Clamp(config.ExpFactor);
        values[7] = Clamp(config.Thickness);
        values[8] = Clamp(config.InputMipLevel);
        values[9] = 1f;
        values[10] = Float(frame, "FrameIndex");
        values[11] = Rand.Next();
        values[12] = width;
        values[13] = height;
        values[14] = proj11;
        values[15] = proj22;
        values[16] = Float(frame, "JitterX");
        values[17] = Float(frame, "JitterY");
        values[20] = far;
        return values;
    }

    static Type Frame()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("ClientPlugin.Shaders.FrameTemporal", false);
            if (type != null)
                return type;
        }
        return null;
    }

    static float Float(Type type, string name)
    {
        if (type == null)
            return 0f;
        try
        {
            var value = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            return value is float f ? f : value is int i ? i : value is uint u ? u : 0f;
        }
        catch
        {
            return 0f;
        }
    }

    static float Scale(Type type, int index)
    {
        if (type == null)
            return 0f;
        try
        {
            var value = type.GetProperty("ProjScale", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (value == null)
                return 0f;
            var field = index == 0 ? "X" : "Y";
            var part = value.GetType().GetField(field)?.GetValue(value) ?? value.GetType().GetProperty(field)?.GetValue(value);
            return part is float f ? f : 0f;
        }
        catch
        {
            return 0f;
        }
    }

    static float Clamp(float value) => Math.Max(0f, Math.Min(1000f, value));
}
