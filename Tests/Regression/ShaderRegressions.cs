using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.D3DCompiler;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using Vector4 = System.Numerics.Vector4;

static class ShaderRegressions
{
    public static void Run(string root)
    {
        var trace = File.ReadAllText(Path.Combine(root, "Assets/AnomalyPack/Fullscreen/AfterLighting/Trace.hlsl"));
        var temporal = File.ReadAllText(Path.Combine(root, "ClientPlugin/Shaders/SSGI/Denoiser/temporal.hlsl"));
        var common = File.ReadAllText(Path.Combine(root, "ClientPlugin/Shaders/SSGI/common.hlsli"));
        var cases = new List<(float Start, float End, int Cover, uint Mask)>();
        for (var cover = 0; cover <= 9; cover++)
        {
            cases.Add((0, 0, cover, cover >= 6 && cover < 8 ? 1u : 0));
            cases.Add((0.5f, 0.5f, cover, cover >= 6 && cover < 8 ? 1u << 16 : 0));
            cases.Add((1, 1, cover, cover >= 6 && cover < 8 ? 1u << 31 : 0));
            cases.Add((0, 1, cover, cover >= 6 ? uint.MaxValue : 0));
            cases.Add((0.25f, 0.5f, cover, cover >= 6 ? 0xFF00u : 0));
        }
        var colors = new[] { (1f, 0f, 1f), (1f, 1f, 1f), (100f, 1f, 12f), (0f, 1f, 0f), (8f, .1f, 1.2f), (1f, 1e-4f, 1f) };
        static string F(float x) => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        var source = "static const uint SectorCount = 32;\n" + Function(trace, "ForegroundSectorMask") +
            Function(common, "luminance") + Function(temporal, "ClampFirefly") +
            $"static const float2 horizons[{cases.Count}] = {{{string.Join(",", cases.Select(c => $"float2({F(c.Start)},{F(c.End)})"))}}};\n" +
            $"static const int covers[{cases.Count}] = {{{string.Join(",", cases.Select(c => c.Cover))}}};\n" +
            $"static const float2 colors[{colors.Length}] = {{{string.Join(",", colors.Select(c => $"float2({F(c.Item1)},{F(c.Item2)})"))}}};\n" +
            $"RWStructuredBuffer<uint4> Output : register(u0);\n[numthreads(1,1,1)] void main(uint3 id: SV_DispatchThreadID) {{ uint mask = id.x < {cases.Count} ? ForegroundSectorMask(horizons[id.x], covers[id.x]) : 0; float color = id.x < {colors.Length} ? ClampFirefly(colors[id.x].xxx, colors[id.x].y).x : 0; Output[id.x] = uint4(mask,countbits(mask),asuint(color),0); }}";
        using var device = new Device(DriverType.Warp, DeviceCreationFlags.None);
        using var bytecode = ShaderBytecode.Compile(source, "main", "cs_5_0", ShaderFlags.OptimizationLevel3);
        using var shader = new ComputeShader(device, bytecode);
        using var output = new Buffer(device, new BufferDescription { SizeInBytes = cases.Count * 16, StructureByteStride = 16, BindFlags = BindFlags.UnorderedAccess, OptionFlags = ResourceOptionFlags.BufferStructured, Usage = ResourceUsage.Default });
        using var uav = new UnorderedAccessView(device, output);
        using var staging = new Buffer(device, new BufferDescription { SizeInBytes = cases.Count * 16, CpuAccessFlags = CpuAccessFlags.Read, Usage = ResourceUsage.Staging });
        var context = device.ImmediateContext;
        context.ComputeShader.Set(shader); context.ComputeShader.SetUnorderedAccessView(0, uav);
        context.Dispatch(cases.Count, 1, 1);
        context.ComputeShader.SetUnorderedAccessView(0, null); context.CopyResource(output, staging);
        context.MapSubresource(staging, MapMode.Read, MapFlags.None, out DataStream stream);
        try
        {
            for (var i = 0; i < cases.Count; i++)
            {
                var mask = stream.Read<uint>(); var bits = stream.Read<uint>();
                var color = stream.Read<float>(); stream.Read<uint>();
                if (mask != cases[i].Mask || bits != System.Numerics.BitOperations.PopCount(cases[i].Mask))
                    throw new Exception($"HLSL sector mask case {i}: 0x{mask:X8} != 0x{cases[i].Mask:X8}");
                if (i < colors.Length && (!float.IsFinite(color) || Math.Abs(color - colors[i].Item3) > 1e-5))
                    throw new Exception($"HLSL firefly case {i}: {color} != {colors[i].Item3}");
            }
        }
        finally { context.UnmapSubresource(staging, 0); stream.Dispose(); }
        Console.WriteLine("PASS: 50 production HLSL sector-mask cases and six sparse/coherent/outlier clamp cases on D3D11 WARP.");
        Temporal(device, root);
    }

    static void Temporal(Device device, string root)
    {
        var compiler = new ClientPlugin.Common.FileShaderCompiler(root, "");
        using var shader = compiler.CompileCompute(device, "Tests/Regression/TemporalProbe.hlsl", "main", new ShaderMacro("VARIANCE_GUIDED", "1"));
        var constants = new float[44];
        foreach (var index in new[] { 0, 5, 10, 15, 16, 21, 26, 31 }) constants[index] = 1;
        constants[32] = constants[33] = constants[34] = constants[35] = 3;
        constants[36] = 16; constants[38] = 10000;
        using var cb = Buffer.Create(device, BindFlags.ConstantBuffer, constants);
        var context = device.ImmediateContext;
        foreach (var scenario in new[] { "coherent new light", "sparse hit", "bright outlier", "invalid history" })
        {
            var samples = Enumerable.Repeat(new Vector4(1, 1, 1, 0), 9).ToArray();
            if (scenario == "sparse hit") { Array.Fill(samples, Vector4.Zero); samples[4] = new Vector4(1, 1, 1, 0); }
            if (scenario == "bright outlier") samples[4] = new Vector4(100, 100, 100, 0);
            using var source = Texture(device, samples);
            using var normals = Texture(device, Enumerable.Repeat(new Vector4(.5f, .5f, 0, 0), 9).ToArray());
            using var depth = Texture(device, Enumerable.Repeat(new Vector4(5, 0, 0, 0), 9).ToArray());
            using var history = Texture(device, new Vector4[9]);
            using var moments = Texture(device, Enumerable.Repeat(new Vector4(0, 0, 16, 0), 9).ToArray());
            using var normalsSrv = new ShaderResourceView(device, normals);
            using var depthSrv = new ShaderResourceView(device, depth);
            using var historySrv = new ShaderResourceView(device, history);
            using var sourceSrv = new ShaderResourceView(device, source);
            using var momentsSrv = new ShaderResourceView(device, moments);
            using var output = new Buffer(device, new BufferDescription { SizeInBytes = 9 * 16, StructureByteStride = 16, BindFlags = BindFlags.UnorderedAccess, OptionFlags = ResourceOptionFlags.BufferStructured, Usage = ResourceUsage.Default });
            using var uav = new UnorderedAccessView(device, output);
            using var staging = new Buffer(device, new BufferDescription { SizeInBytes = 9 * 16, CpuAccessFlags = CpuAccessFlags.Read, Usage = ResourceUsage.Staging });
            context.ComputeShader.Set(shader); context.ComputeShader.SetConstantBuffer(0, cb);
            context.ComputeShader.SetShaderResource(1, normalsSrv); context.ComputeShader.SetShaderResource(4, depthSrv);
            context.ComputeShader.SetShaderResource(5, historySrv); context.ComputeShader.SetShaderResource(6, sourceSrv);
            context.ComputeShader.SetShaderResource(7, historySrv);
            context.ComputeShader.SetShaderResource(8, scenario == "invalid history" ? null : depthSrv);
            context.ComputeShader.SetShaderResource(9, normalsSrv); context.ComputeShader.SetShaderResource(10, momentsSrv);
            context.ComputeShader.SetUnorderedAccessView(0, uav); context.Dispatch(3, 3, 1);
            context.ComputeShader.SetUnorderedAccessView(0, null); context.CopyResource(output, staging);
            context.MapSubresource(staging, MapMode.Read, MapFlags.None, out DataStream stream);
            try
            {
                stream.Position = 4 * 16;
                var actual = stream.Read<Vector4>();
                var expected = scenario == "invalid history" ? 1f : scenario == "bright outlier" ? 12f / 17 : 1f / 17;
                if (!float.IsFinite(actual.W) || Math.Abs(actual.X - expected) > 1e-4 || Math.Abs(actual.Y - expected) > 1e-4 || Math.Abs(actual.Z - expected) > 1e-4)
                    throw new Exception($"Full temporal {scenario}: {actual} != {expected}");
            }
            finally { context.UnmapSubresource(staging, 0); stream.Dispose(); }
            for (var slot = 0; slot <= 10; slot++) context.ComputeShader.SetShaderResource(slot, null);
        }
        context.ComputeShader.SetConstantBuffer(0, null); context.ComputeShader.Set(null);
        Console.WriteLine("PASS: complete production temporal ps() on WARP: coherent light over black mature history, isolated hit, 12x outlier rejection and null-depth first frame.");
    }

    static Texture2D Texture(Device device, Vector4[] values)
    {
        using var stream = new DataStream(values.Length * 16, true, true);
        stream.WriteRange(values); stream.Position = 0;
        return new Texture2D(device, new Texture2DDescription { Width = 3, Height = 3, ArraySize = 1, MipLevels = 1,
            Format = SharpDX.DXGI.Format.R32G32B32A32_Float, SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
            BindFlags = BindFlags.ShaderResource, Usage = ResourceUsage.Default }, new DataRectangle(stream.DataPointer, 3 * 16));
    }
    static string Function(string source, string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(source, @"(?m)^\w+ " + name + @"\(");
        if (!match.Success) throw new Exception("Missing production function: " + name);
        var body = source.IndexOf('{', match.Index); var end = body + 1; var depth = 1;
        for (; depth > 0; end++) { if (source[end] == '{') depth++; else if (source[end] == '}') depth--; }
        return source.Substring(match.Index, end - match.Index) + "\n";
    }
}
