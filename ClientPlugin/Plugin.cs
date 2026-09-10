using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ClientPlugin.Anomaly;
using ClientPlugin.Config;
using ClientPlugin.Gui;
using ClientPlugin.RichHud;
using ClientPlugin.SSGI;
using Sandbox.Graphics.GUI;
using VRage.FileSystem;
using VRage.Input;
using VRage.Plugins;
using VRage.Utils;
using VRageRender;

#if !DEV
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
#endif

namespace ClientPlugin;

public class Plugin : IPlugin
{
    public static string ShaderDirectory { get; private set; }
    public static SSGIConfig SSGIConfig { get; private set; }
    public static string AnomalyPackRoot { get; private set; }

    static string _assetFolder;

    public void LoadAssets(string path)
    {
        SetShaderDirectory(path);
    }

    public void LoadAssets(IReadOnlyDictionary<string, string> assets)
    {
        if (assets == null)
            return;
        if (assets.TryGetValue("AssetFolder", out var folder) && !string.IsNullOrEmpty(folder))
            SetShaderDirectory(folder);
        else if (assets.TryGetValue("Shaders", out var shaders) && !string.IsNullOrEmpty(shaders))
            SetShaderDirectory(shaders);

        if (assets.TryGetValue("AnomalyPack", out var pack) && !string.IsNullOrEmpty(pack))
            AnomalyPackRoot = pack;

        EnsureShaderDirectory();
        EnsureConfig();
        TryRegisterPack();
        AnomalyTerminalHook.TryInstall();
    }

    public void Init(object gameInstance)
    {
        EnsureShaderDirectory();
        EnsureConfig();

        TryRegisterPack();
        AnomalyHook.Probe();
        if (!AnomalyHook.RegistryFound)
        {
            MyLog.Default.WriteLine("SSGI: Anomaly Shader Framework was not found. Enable Anomaly and restart.");
            AnomalyTerminalHook.TryInstall();
            return;
        }

        if (!AnomalyHook.PackRegistered)
        {
            MyLog.Default.WriteLine("SSGI: Anomaly pack registration failed; GI pass not started.");
            AnomalyTerminalHook.TryInstall();
            return;
        }

        if (string.IsNullOrEmpty(ShaderDirectory) || !Directory.Exists(ShaderDirectory))
        {
            MyLog.Default.WriteLine("SSGI: shader directory missing; GI pass not started.");
            AnomalyTerminalHook.TryInstall();
            return;
        }

        SSGIPass.Init();
        AnomalyTerminalHook.TryInstall();
    }

    public void OpenConfigDialog() => MyGuiSandbox.AddScreen(new GuiScreenSSGIConfig(SSGIConfig));

    public void Update()
    {
        AnomalyTerminalHook.TryInstall();
        SSGIConfig?.FlushPending();
#if DEV
        if (MyInput.Static.IsAnyShiftKeyPressed() && MyInput.Static.IsNewKeyPressed(MyKeys.OemPipe))
        {
            MyRender11.EnqueueUpdate(() => SSGIPass.ReloadShaders());
        }
#endif
    }

    public void Dispose()
    {
        SSGIConfig?.FlushPending(true);
    }

    static void EnsureConfig()
    {
        if (SSGIConfig != null)
            return;
        string storageDirectory = Path.Combine(MyFileSystem.UserDataPath, "Storage", "Prism");
        SSGIConfig = SSGIConfig.LoadOrCreate(Path.Combine(storageDirectory, "ssgi2.json"));
    }

    static void TryRegisterPack()
    {
        var root = ResolvePackRoot();
        if (string.IsNullOrEmpty(root))
            return;
        AnomalyHook.TryRegisterPack(root);
    }

    static string ResolvePackRoot()
    {
        if (!string.IsNullOrEmpty(AnomalyPackRoot) && Directory.Exists(AnomalyPackRoot))
            return AnomalyPackRoot;
        if (!string.IsNullOrEmpty(_assetFolder))
        {
            var nested = Path.Combine(_assetFolder, "AnomalyPack");
            if (Directory.Exists(nested))
                return nested;
        }

        return AnomalyPackRoot;
    }

    static void SetShaderDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            return;
        _assetFolder = path;
        ShaderDirectory = path;
    }

    static void EnsureShaderDirectory()
    {
        if (!string.IsNullOrEmpty(ShaderDirectory) && Directory.Exists(ShaderDirectory))
            return;

        if (!string.IsNullOrEmpty(_assetFolder) && Directory.Exists(_assetFolder))
        {
            ShaderDirectory = _assetFolder;
            return;
        }

        if (!string.IsNullOrEmpty(AnomalyPackRoot))
        {
            // Assets/AnomalyPack → ClientPlugin/Shaders (same repo layout Pulsar compiles from).
            var fromPack = Path.GetFullPath(Path.Combine(AnomalyPackRoot, "..", "..", "ClientPlugin", "Shaders"));
            if (Directory.Exists(fromPack))
            {
                ShaderDirectory = fromPack;
                MyLog.Default.WriteLine("SSGI: shader directory from pack root: " + fromPack);
                return;
            }
        }

        var fromAsm = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "Shaders");
        if (Directory.Exists(fromAsm))
        {
            ShaderDirectory = fromAsm;
            MyLog.Default.WriteLine("SSGI: shader directory from assembly: " + fromAsm);
        }
    }
}
