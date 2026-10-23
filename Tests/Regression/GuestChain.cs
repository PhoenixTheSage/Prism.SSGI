using ClientPlugin.Anomaly;
using ClientPlugin.SSGI;
using Newtonsoft.Json.Linq;

static class GuestChainRegressions
{
    public static void Run(string root)
    {
        var passes = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(root, "Assets/AnomalyPack/anomaly.json")))["passes"];
        var owner = passes.Single(p => (string)p["history"] == "ssgi.colorHistory");
        Check((string)owner["id"] == "prism.ssgi.colorHistory", "filtered history has a separate owner");
        Check(passes.IndexOf(owner) > passes.IndexOf(passes.Single(p => (string)p["id"] == "prism.ssgi.atrous6")), "history capture follows all filters");
        SsgiGuest.Tick();
        foreach (int count in new[] { 0, 3, 7, 1, 0 })
        {
            var config = ClientPlugin.Plugin.SSGIConfig;
            config.DenoiserBlurIterations = count;
            config.ResolutionScale = count == 7 ? .25f : .5f;
            config.Enabled = true;
            AnomalyHook.Prepare(null);
            string source = "pass.prism.ssgi." + (count == 0 ? "temporal" : "atrous" + (count - 1));
            Check(AnomalyHook.Inputs[("prism.ssgi.colorHistory", 7)] == source, "capture selects final filter at " + count);
            Check(AnomalyHook.Inputs[("prism.ssgi.composite", 7)] == source, "composite and retained color match");
            Check(AnomalyHook.Scales["prism.ssgi.colorHistory"] == config.ResolutionScale, "history follows trace scale");
            Check(AnomalyHook.Enabled["prism.ssgi.colorHistory"], "enabled capture");
            config.Enabled = false;
            AnomalyHook.Prepare(null);
            Check(!AnomalyHook.Enabled["prism.ssgi.colorHistory"], "master switch disables capture");
        }
        SsgiGuest.DisableForDx11();
        Check(!AnomalyHook.Enabled["prism.ssgi.colorHistory"], "DX11 disables guest capture");
        SsgiGuest.Dispose();
        Console.WriteLine("PASS: production guest preparation retains the displayed filtered color across 0/3/7/1/0 iterations, quarter/half scale, disable and DX11 handoff.");
    }
    static void Check(bool value, string name) { if (!value) throw new Exception(name); }
}

namespace ClientPlugin
{
    static class Plugin { internal static Config.SSGIConfig SSGIConfig = new("unused-guest-regression.json"); }
}

namespace ClientPlugin.Anomaly
{
    // Record the production adapter's host calls without loading a game or GPU.
    static class AnomalyHook
    {
        public const string TraceProgramId = "prism.ssgi.trace";
        public static Action<object> Prepare;
        public static readonly Dictionary<(string, int), string> Inputs = new();
        public static readonly Dictionary<string, float> Scales = new();
        public static readonly Dictionary<string, bool> Enabled = new();
        public static bool TryRegisterPortablePass(string id, Action<object> action) { Prepare = action; return true; }
        public static void UnregisterPortablePass(string id) { Prepare = null; }
        public static void RequestSrv(string id, string catalog, int slot) => Inputs[(id, slot)] = catalog;
        public static void SetEnabled(string id, bool enabled) => Enabled[id] = enabled;
        public static void SetScale(string id, float scale) => Scales[id] = scale;
        public static void SetUniforms(string id, float[] values) { }
    }
}
