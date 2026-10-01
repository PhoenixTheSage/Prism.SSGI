using ClientPlugin.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
ConfigRegressions.Run();
LifecycleFixture.LifecycleRegression.Run();
ShaderRegressions.Run(root);
Console.WriteLine("PASS: SSGI config, extracted lifecycle methods, and production HLSL helpers on D3D11 WARP.");

static class ConfigRegressions
{
    public static void Run()
    {
        var file = Path.Combine(Path.GetTempPath(), "Prism.SSGI.Regression-" + Guid.NewGuid() + ".json");
        try
        {
            foreach (var preset in new[] { SSGIQualityPreset.Low, SSGIQualityPreset.Medium, SSGIQualityPreset.High })
            {
                var config = new SSGIConfig(file);
                SSGIConfig.Presets[(int)preset].ApplyTo(config);
                Check(config.DetectPreset() == preset, "preset identity");
                var expected = preset == SSGIQualityPreset.Low ? 0.25f : 0.5f;
                var legacy = JObject.FromObject(config);
                legacy.Remove("ResolutionScale");
                File.WriteAllText(file, legacy.ToString());
                Check(SSGIConfig.LoadOrCreate(file).TraceScale() == expected, "legacy preset migration");
                config.GIIntensity += 0.1f;
                Check(config.TraceScale() == expected, "intensity preserves scale");
                config.Save(); config.FlushPending(true);
                Check(SSGIConfig.LoadOrCreate(file).TraceScale() == expected, "custom round-trip scale");
            }
            var low = new SSGIConfig(file);
            foreach (var edit in new Action<SSGIConfig>[] {
                c => c.GIIntensity = 5.1f, c => c.InputMipLevel = 3, c => c.SliceCount = 2,
                c => c.StepCount = 12, c => c.Radius = 8, c => c.ExpFactor = 1.7f,
                c => c.Thickness = 2, c => c.DenoiserMaxHistory = 20, c => c.DenoiserBlurIterations = 3,
            })
            {
                SSGIConfig.Presets[0].ApplyTo(low); edit(low);
                Check(low.DetectPreset() == SSGIQualityPreset.Custom && low.TraceScale() == 0.25f, "Low slider independence");
            }
            SSGIConfig.Presets[0].ApplyTo(low); low.GIIntensity = 5.1f;
            var oldCustom = JObject.FromObject(low); oldCustom.Remove("ResolutionScale");
            File.WriteAllText(file, oldCustom.ToString());
            Check(SSGIConfig.LoadOrCreate(file).TraceScale() == 0.5f, "legacy Custom retains old half resolution");
            low.ApplyPreset(SSGIQualityPreset.High);
            Check(low.TraceScale() == 0.5f, "High switches scale");
            low.ApplyPreset(SSGIQualityPreset.Low);
            Check(low.TraceScale() == 0.25f, "Low switches scale");
            low.FlushPending(true);
            Check(SSGIConfig.LoadOrCreate(file).DetectPreset() == SSGIQualityPreset.Low, "preset save and reload");
            Console.WriteLine("PASS: legacy preset/Custom migration, nine Low slider edits, scale switches and JSON round trips.");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
}

namespace VRage.Utils {
    public sealed class MyLog {
        public static MyLog Default { get; } = new();
        public void Info(string message) { }
        public void WriteLine(object message) { }
    }
}
