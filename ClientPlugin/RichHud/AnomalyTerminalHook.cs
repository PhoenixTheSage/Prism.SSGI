using System;
using System.Reflection;
using ClientPlugin.Anomaly;
using ClientPlugin.Config;
using ClientPlugin.SSGI;
using VRage.Utils;

namespace ClientPlugin.RichHud;

/// <summary>
/// Optional bind to Anomaly's Rich HUD extension.
/// Well-known type: <c>ClientPlugin.RichHud.TerminalConfigRegistry</c>.
/// </summary>
internal static class AnomalyTerminalHook
{
    public const string RegistryTypeName = "ClientPlugin.RichHud.TerminalConfigRegistry";
    public const string FolderTitle = "SSGI";
    public const string SettingsPage = "Settings";
    public const string StatusPage = "Status";

    static readonly object Gate = new();
    static bool _installed;

    public static bool TryInstall()
    {
        lock (Gate)
        {
            if (_installed)
                return true;

            var settings = RequestPageUnlocked(SettingsPage);
            if (settings == null)
                return false;

            PopulateSettings(settings);
            var status = RequestPageUnlocked(StatusPage);
            if (status != null)
                PopulateStatus(status);
            _installed = true;
            MyLog.Default.WriteLine("SSGI: Rich HUD page under Anomaly Shaders / " + FolderTitle + " / " + SettingsPage);
            return true;
        }
    }

    static object RequestPageUnlocked(string pageTitle)
    {
        Assembly[] assemblies;
        try
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        catch
        {
            return null;
        }

        foreach (var assembly in assemblies)
        {
            if (assembly == null || assembly == typeof(AnomalyTerminalHook).Assembly)
                continue;

            Type registry;
            try
            {
                registry = assembly.GetType(RegistryTypeName, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                continue;
            }

            var requestFolder = FindStatic(registry, "RequestFolderPage", typeof(string), typeof(string));
            var request = FindStatic(registry, "RequestPage", typeof(string));
            if (requestFolder == null && request == null)
                continue;

            try
            {
                if (requestFolder != null)
                    return requestFolder.Invoke(null, new object[] { FolderTitle, pageTitle });
                if (pageTitle != SettingsPage)
                    return null;
                return request.Invoke(null, new object[] { FolderTitle });
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLine("SSGI: Anomaly RequestPage: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        return null;
    }

    static void PopulateSettings(object page)
    {
        var config = Plugin.SSGIConfig;
        if (config == null)
        {
            MyLog.Default.WriteLine("SSGI: Rich HUD skipped — config not loaded");
            return;
        }

        var type = page.GetType();
        Invoke(type, page, "Category", "SSGI");
        Invoke(type, page, "Checkbox", "Enable SSGI",
            (Func<bool>)(() => config.Enabled),
            (Action<bool>)(v =>
            {
                config.Enabled = v;
                config.Save();
                AnomalyHook.SetEnabled(AnomalyHook.TraceProgramId, v);
            }),
            "Single-bounce screen-space global illumination.");
        Invoke(type, page, "Dropdown", "Quality preset", typeof(SSGIQualityPreset),
            (Func<object>)(() => config.DetectPreset()),
            (Action<object>)(v =>
            {
                if (v is SSGIQualityPreset preset)
                    config.ApplyPreset(preset);
            }),
            "Low / Medium / High write the nine GI sliders. Custom means the sliders were edited.");

        Invoke(type, page, "Category", "Quality");
        Invoke(type, page, "Slider", "GI Intensity", 0f, 10f,
            (Func<float>)(() => config.GIIntensity),
            (Action<float>)(v => Set(config, () => config.GIIntensity = v)),
            "Light intensity multiplier.", 0.1f);
        Invoke(type, page, "IntSlider", "Input Prefiltering", 0, 4,
            (Func<int>)(() => config.InputMipLevel),
            (Action<int>)(v => Set(config, () => config.InputMipLevel = v)),
            "More prefiltering improves temporal stability but increases light leaking.");
        Invoke(type, page, "Slider", "Radius", 0f, 15f,
            (Func<float>)(() => config.Radius),
            (Action<float>)(v => Set(config, () => config.Radius = v)),
            "Approximate range in meters.", 0.1f);
        Invoke(type, page, "IntSlider", "Slices", 1, 32,
            (Func<int>)(() => config.SliceCount),
            (Action<int>)(v => Set(config, () => config.SliceCount = v)),
            "Increases quality at a significant performance cost.");
        Invoke(type, page, "IntSlider", "Steps", 1, 64,
            (Func<int>)(() => config.StepCount),
            (Action<int>)(v => Set(config, () => config.StepCount = v)),
            "Increases quality at a significant performance cost.");
        Invoke(type, page, "Slider", "ExpFactor", 1f, 2f,
            (Func<float>)(() => config.ExpFactor),
            (Action<float>)(v => Set(config, () => config.ExpFactor = v)),
            "Higher values place more samples near the pixel.", 0.05f);
        Invoke(type, page, "Slider", "Thickness", 0f, 10f,
            (Func<float>)(() => config.Thickness),
            (Action<float>)(v => Set(config, () => config.Thickness = v)),
            "Horizon thickness in meters.", 0.1f);
        Invoke(type, page, "IntSlider", "Temporal History", 0, 50,
            (Func<int>)(() => config.DenoiserMaxHistory),
            (Action<int>)(v => Set(config, () => config.DenoiserMaxHistory = v)),
            "Maximum accumulated history length.");
        Invoke(type, page, "IntSlider", "Blur Iterations", 0, 7,
            (Func<int>)(() => config.DenoiserBlurIterations),
            (Action<int>)(v => Set(config, () => config.DenoiserBlurIterations = v)),
            "À-trous blur iterations.");
        Invoke(type, page, "Button", "Show Status",
            (Action)SSGIStatus.Show,
            "Compile, Anomaly bind, pass size, and last skip reason.");
    }

    static void PopulateStatus(object page)
    {
        var type = page.GetType();
        Invoke(type, page, "Category", "Debug");
        Invoke(type, page, "Button", "Show Status",
            (Action)SSGIStatus.Show,
            "Compile, Anomaly bind, pass size, and last skip reason.");
    }

    static void Set(SSGIConfig config, Action apply)
    {
        apply();
        config.Save();
    }

    static void Invoke(Type type, object instance, string name, params object[] args)
    {
        var method = FindInstance(type, name, args);
        if (method == null)
        {
            MyLog.Default.WriteLine("SSGI: Anomaly terminal missing " + name);
            return;
        }

        method.Invoke(instance, PadDefaults(method, args));
    }

    static MethodInfo FindStatic(Type type, string name, params Type[] parameters)
    {
        if (type == null)
            return null;
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
    }

    static MethodInfo FindInstance(Type type, string name, object[] args)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var method in type.GetMethods(flags))
        {
            if (method.Name != name || method.IsGenericMethodDefinition)
                continue;
            var parameters = method.GetParameters();
            if (!ArgumentsMatch(parameters, args))
                continue;
            return method;
        }

        return null;
    }

    static object[] PadDefaults(MethodInfo method, object[] args)
    {
        var parameters = method.GetParameters();
        if (args.Length == parameters.Length)
            return args;
        var padded = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            padded[i] = i < args.Length ? args[i] : parameters[i].DefaultValue;
        return padded;
    }

    static bool ArgumentsMatch(ParameterInfo[] parameters, object[] args)
    {
        if (args.Length > parameters.Length)
            return false;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i >= args.Length)
                return parameters[i].IsOptional || parameters[i].HasDefaultValue;
            var value = args[i];
            var expected = parameters[i].ParameterType;
            if (value == null)
            {
                if (expected.IsValueType && Nullable.GetUnderlyingType(expected) == null)
                    return false;
                continue;
            }

            if (!expected.IsInstanceOfType(value))
                return false;
        }

        return true;
    }
}
