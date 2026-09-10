using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using VRage.Utils;

namespace ClientPlugin.Config;

public class SSGIConfig
{
    [ConfigProperty("Enable SSGI")]
    public bool Enabled { get; set; } = true;

    [FloatConfigProperty("GI Intensity", 0, 10, 5, "Light intensity multiplier.")]
    public float GIIntensity { get; set; } = 5;

    [IntConfigProperty("Input Prefiltering", 0, 4, 3, "More prefiltering improves temporal stability but increases light leaking.")]
    public int InputMipLevel { get; set; } = 3;

    [IntConfigProperty("Slices", 1, 32, 2, "Increases quality at a significant performance cost.")]
    public int SliceCount { get; set; } = 2;

    [IntConfigProperty("Steps", 1, 64, 16, "Increases quality at a significant performance cost.")]
    public int StepCount { get; set; } = 16;

    [FloatConfigProperty("Radius", 0, 15, 7.5f, "Approximate range in meters.")]
    public float Radius { get; set; } = 10;

    [FloatConfigProperty("ExpFactor", 1, 2, 1.5f, "Controls sample distribution.\nHigher values cause more samples to be placed near the pixel.")]
    public float ExpFactor { get; set; } = 1.5f;

    [FloatConfigProperty("Thickness", 0, 10, 1)]
    public float Thickness { get; set; } = 1;

    [IntConfigProperty("Denoiser Temporal History", 0, 50, 20, "Sets the maximum accumulated history length.\nHigher values increase stability at the cost of slower reaction to lighting changes.")]
    public int DenoiserMaxHistory { get; set; } = 20;

    //[IntConfigProperty("Denoiser Blur Radius", 0, 32, 16, "Sets the maximum blur radius.\nHigher values can lower noise but causes the lighting to look flatter.")]
    public float DenoiserBlurRadius { get; set; } = 16;

    [IntConfigProperty("Denoiser Blur Iterations", 0, 7, 5, "Sets the maximum blur iterations.\nHigher values can lower noise but causes the lighting to look flatter.")]
    public int DenoiserBlurIterations { get; set; } = 5;

    public SSGIQualityPreset DetectPreset()
    {
        for (var i = 0; i < Presets.Length; i++)
        {
            if (Presets[i].Matches(this))
                return (SSGIQualityPreset)i;
        }

        return SSGIQualityPreset.Custom;
    }

    public void ApplyPreset(SSGIQualityPreset preset)
    {
        if (preset == SSGIQualityPreset.Custom)
            return;
        var index = (int)preset;
        if (index < 0 || index >= Presets.Length)
            return;
        Presets[index].ApplyTo(this);
        Save();
    }

    public static readonly QualityPreset[] Presets =
    {
        new QualityPreset // Low
        {
            GIIntensity = 5,
            InputMipLevel = 4,
            SliceCount = 1,
            StepCount = 8,
            Radius = 5.0f,
            ExpFactor = 1.5f,
            Thickness = 1.0f,
            DenoiserMaxHistory = 24,
            DenoiserBlurIterations = 5,
        },
        new QualityPreset // Medium
        {
            GIIntensity = 5,
            InputMipLevel = 3,
            SliceCount = 2,
            StepCount = 16,
            Radius = 7.5f,
            ExpFactor = 1.5f,
            Thickness = 1.0f,
            DenoiserMaxHistory = 20,
            DenoiserBlurIterations = 5,
        },
        new QualityPreset // High
        {
            GIIntensity = 5,
            InputMipLevel = 3,
            SliceCount = 4,
            StepCount = 32,
            Radius = 10.0f,
            ExpFactor = 1.5f,
            Thickness = 1.0f,
            DenoiserMaxHistory = 20,
            DenoiserBlurIterations = 4,
        },
    };

    public struct QualityPreset
    {
        public float GIIntensity;
        public int InputMipLevel;
        public int SliceCount;
        public int StepCount;
        public float Radius;
        public float ExpFactor;
        public float Thickness;
        public int DenoiserMaxHistory;
        public int DenoiserBlurIterations;

        public bool Matches(SSGIConfig config)
        {
            return config.GIIntensity == GIIntensity
                && config.InputMipLevel == InputMipLevel
                && config.SliceCount == SliceCount
                && config.StepCount == StepCount
                && config.Radius == Radius
                && config.ExpFactor == ExpFactor
                && config.Thickness == Thickness
                && config.DenoiserMaxHistory == DenoiserMaxHistory
                && config.DenoiserBlurIterations == DenoiserBlurIterations;
        }

        public void ApplyTo(SSGIConfig config)
        {
            config.GIIntensity = GIIntensity;
            config.InputMipLevel = InputMipLevel;
            config.SliceCount = SliceCount;
            config.StepCount = StepCount;
            config.Radius = Radius;
            config.ExpFactor = ExpFactor;
            config.Thickness = Thickness;
            config.DenoiserMaxHistory = DenoiserMaxHistory;
            config.DenoiserBlurIterations = DenoiserBlurIterations;
        }
    }

    const int DebounceMs = 400;

    readonly string _filePath;
    readonly object _saveGate = new();
    readonly object _writeGate = new();
    bool _dirty;
    int _lastRequestTick;
    int _writesInFlight;

    public SSGIConfig(string filePath)
    {
        _filePath = filePath;
    }

    public static SSGIConfig LoadOrCreate(string filePath)
    {
        if (!File.Exists(filePath))
        {
            MyLog.Default.Info($"{typeof(SSGIConfig).FullName}: Config not found, initializing default values. path={filePath}.");
            return CreateDefaultAndSave(filePath);
        }

        try
        {
            JsonSerializer serializer = new JsonSerializer();
            using (var sr = new StreamReader(filePath))
            using (var jr = new JsonTextReader(sr))
            {
                var config = new SSGIConfig(filePath);
                serializer.Populate(jr, config);
                return config;
            }
        }
        catch (Exception e)
        {
            MyLog.Default.Info($"{typeof(SSGIConfig).FullName}: Could not load config, initializing default values. path={filePath}, {e}");
            return CreateDefaultAndSave(filePath);
        }
    }

    private static SSGIConfig CreateDefaultAndSave(string filePath)
    {
        var conf = new SSGIConfig(filePath);
        conf.Save();
        conf.FlushPending(true);
        return conf;
    }

    /// <summary>
    /// Marks dirty. Rich HUD sliders fire this while dragging; write from
    /// <see cref="FlushPending"/> on a worker so the HUD / Update thread
    /// does not hit the disk.
    /// </summary>
    public void Save()
    {
        lock (_saveGate)
        {
            _dirty = true;
            _lastRequestTick = Environment.TickCount;
        }
    }

    public void FlushPending(bool force = false)
    {
        var shouldWrite = false;
        lock (_saveGate)
        {
            if (_dirty)
            {
                if (!force && unchecked(Environment.TickCount - _lastRequestTick) < DebounceMs)
                    return;
                _dirty = false;
                shouldWrite = true;
            }
            else if (!force)
            {
                return;
            }
        }

        if (!force)
        {
            Interlocked.Increment(ref _writesInFlight);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { Write(); }
                finally { Interlocked.Decrement(ref _writesInFlight); }
            });
            return;
        }

        var spins = 0;
        while (Volatile.Read(ref _writesInFlight) > 0 && spins++ < 2000)
            Thread.Sleep(1);
        if (shouldWrite)
            Write();
    }

    void Write()
    {
        try
        {
            var serializer = new JsonSerializer
            {
                Formatting = Formatting.Indented,
            };

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            lock (_writeGate)
            {
                using (var sw = new StreamWriter(_filePath, false))
                    serializer.Serialize(sw, this);
            }
        }
        catch (Exception e)
        {
            MyLog.Default.Info($"{typeof(SSGIConfig).FullName}: Could not save config. {e}");
        }
    }
}
