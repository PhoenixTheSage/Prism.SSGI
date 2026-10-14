namespace ClientPlugin.SSGI;

public static class SsgiDx11Entry
{
    public static void Init() => SSGIPass.Init();

    public static void Reload() => SSGIPass.ReloadShaders();
}
