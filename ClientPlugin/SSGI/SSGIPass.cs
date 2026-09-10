using System;
using System.Runtime.InteropServices;
using ClientPlugin.Anomaly;
using ClientPlugin.Common;
using ClientPlugin.Config;
using Sandbox;
using Sandbox.ModAPI;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using VRage.Render.Scene;
using VRage.Render11.Common;
using VRage.Render11.RenderContext;
using VRage.Render11.Resources;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace ClientPlugin.SSGI;

public static class SSGIPass
{
    [StructLayout(LayoutKind.Sequential)]
    struct DenoiserCb
    {
        public Matrix ViewMatrix;
        public Matrix PrevViewMatrix;
        public Vector2 ScreenSize;
        public float MaxHistory;
        public int AtrousStepSize;
        public float Farplane;
        private uint _pad0;
        private uint _pad1;
        private uint _pad2;
    }

    static bool _compileError;
    static bool _ready;
    static PixelShader _psSvgfTemporal;
    static PixelShader _psSvgfAtrous;
    static PixelShader _psCopyBlend;
    static IConstantBuffer _cbv;
    static IRtvTexture _historyTexture;
    static IRtvTexture _prevMomentsAndHistoryLength;
    static IRtvTexture _prevDepthTex;
    static IRtvTexture _prevGBuffer1;
    static object _historyPublished;
    static object _prevDepthPublished;
    static readonly Random Rand = new();
    static Matrix _prevViewMatrix = Matrix.Identity;
    static Vector2I _size;

    public static void Init()
    {
        if (_ready)
            return;

        RecreateTargets();
        ReloadShaders();
        AnomalyHook.RegisterLifetime(OnResolutionChanged, OnDeviceEnd);
        AnomalyHook.RequestSrv(AnomalyHook.TraceProgramId, AnomalyHook.LitMipsName, 7);
        AnomalyHook.SetEnabled(AnomalyHook.TraceProgramId, Plugin.SSGIConfig.Enabled);
        RegisterOwnedPasses();
        _ready = true;
    }

    static void RegisterOwnedPasses()
    {
        AnomalyHook.TryRegisterOwnedPass(
            AnomalyHook.MipsPassId, "AfterLighting", 0, AnomalyHook.TemporalInColor,
            BeforeFullscreen, AnomalyHook.PhaseBeforeFullscreen);
        AnomalyHook.TryRegisterOwnedPass(
            AnomalyHook.SvgfPassId, "AfterLighting", 0, AnomalyHook.TemporalInColor,
            AfterFullscreen, AnomalyHook.PhaseAfterFullscreen);
    }

    static void OnResolutionChanged()
    {
        RecreateTargets();
    }

    static void OnDeviceEnd()
    {
        DisposeTargets();
        _ready = false;
    }

    static unsafe void RecreateTargets()
    {
        DisposeTargets();
        Vector2I res = MyRender11.ResolutionI;
        _size = res;
        _cbv = MyManagers.Buffers.CreateConstantBuffer("Prism.SSGI.CbvDenoiser",
            MathHelper.Align(sizeof(DenoiserCb), 16), usage: ResourceUsage.Dynamic, isGlobal: true);
        _historyTexture = MyManagers.RwTextures.CreateRtv("Prism.SSGI.RtvHistory", res.X, res.Y,
            Format.R16G16B16A16_Float);
        _prevMomentsAndHistoryLength = MyManagers.RwTextures.CreateRtv(
            "Prism.SSGI.RtvPrevMomentsAndHistoryLength", res.X, res.Y, Format.R16G16B16A16_Float);
        _prevDepthTex = MyManagers.RwTextures.CreateRtv("Prism.SSGI.RtvPrevDepth", res.X, res.Y, Format.R32_Float);
        _prevGBuffer1 = MyManagers.RwTextures.CreateRtv("Prism.SSGI.RtvPrevGBuffer1", res.X, res.Y,
            MyGBuffer.Main.GBuffer1.Format);

        _historyPublished ??= AnomalyHook.TryCreatePublishedBuffer();
        _prevDepthPublished ??= AnomalyHook.TryCreatePublishedBuffer();
    }

    static void DisposeTargets()
    {
        _cbv = null;
        _historyTexture = null;
        _prevMomentsAndHistoryLength = null;
        _prevDepthTex = null;
        _prevGBuffer1 = null;
    }

    public static void ReloadShaders()
    {
        _psSvgfTemporal?.Dispose();
        _psSvgfAtrous?.Dispose();
        _psCopyBlend?.Dispose();

        var compiler = new FileShaderCompiler(Plugin.ShaderDirectory, MyShaderCompiler.ShadersPath);
        SharpDX.Direct3D11.Device device = MyRender11.DeviceInstance;
        try
        {
            _psSvgfTemporal = compiler.CompilePixel(device, "SSGI/Denoiser/temporal.hlsl", "ps",
                new ShaderMacro("VARIANCE_GUIDED", 1));
            _psSvgfAtrous = compiler.CompilePixel(device, "SSGI/Denoiser/atrous.hlsl", "ps",
                new ShaderMacro("VARIANCE_GUIDED", 1));
            _psCopyBlend = compiler.CompilePixel(device, "SSGI/Denoiser/copyblend.hlsl", "ps",
                new ShaderMacro("VARIANCE_GUIDED", 1));
            _compileError = false;
        }
        catch (Exception e)
        {
#if DEV
            _compileError = true;
            MyLog.Default.WriteLine(e);
            MySandboxGame.Static.Invoke(() =>
            {
                try
                {
                    MyAPIGateway.Utilities.ShowMessage("Prism.SSGI", e.ToString());
                }
                catch
                {
                    // ignored
                }
            }, "Prism.SSGI");
#else
            throw;
#endif
        }
    }

    static void BeforeFullscreen(object ctx)
    {
        if (_compileError || !_ready)
            return;

        var config = Plugin.SSGIConfig;
        AnomalyHook.SetEnabled(AnomalyHook.TraceProgramId, config.Enabled);
        WriteTraceUniforms();
    }

    static void AfterFullscreen(object ctx)
    {
        if (_compileError || !_ready || !Plugin.SSGIConfig.Enabled)
            return;

        RenderTraceBind.Begin("SSGI.AfterFullscreen");
        try
        {
            AfterFullscreenCore(ctx);
        }
        catch (Exception e)
        {
            RenderTraceBind.Dump("SSGI.AfterFullscreen", e);
            throw;
        }
        finally
        {
            RenderTraceBind.End("SSGI.AfterFullscreen");
        }
    }

    static void AfterFullscreenCore(object ctx)
    {
        EnsureSize();
        var rc = AnomalyHook.GetRenderContext(ctx);
        if (rc == null || !rc.IsInitialized)
            return;
        var noisySrv = AnomalyHook.TryGetSrv(AnomalyHook.TraceOutputName);
        if (noisySrv == null)
            return;

        IBorrowedRtvTexture borrowed = null;
        IRtvTexture noisyRtv = noisySrv as IRtvTexture;
        if (noisyRtv == null)
        {
            borrowed = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI.TraceCopy",
                _size.X, _size.Y, Format.R16G16B16A16_Float);
            CopyReplace(rc, noisySrv, borrowed);
            noisyRtv = borrowed;
        }

        UpdateDenoiserCb(rc, 0, true);
        BindCommon(rc);
        DenoiseVarianceGuided(rc, noisyRtv, _historyTexture, MyGBuffer.Main.LBuffer);
        borrowed?.Release();

        rc.CopyResource(MyGBuffer.Main.GBuffer1, _prevGBuffer1);
        var linearDepth = AnomalyHook.TryGetSrv("linearDepth");
        if (linearDepth != null)
            CopyReplace(rc, linearDepth, _prevDepthTex);

        AnomalyHook.TryPublishExisting(_historyPublished, AnomalyHook.HistoryName, _historyTexture, _size.X, _size.Y);
        AnomalyHook.TryPublishExisting(_prevDepthPublished, AnomalyHook.PrevDepthName, _prevDepthTex, _size.X, _size.Y);
        Unbind(rc);
    }

    static void EnsureSize()
    {
        if (_size != MyRender11.ResolutionI)
            RecreateTargets();
    }

    static void WriteTraceUniforms()
    {
        var env = MyRender11.Environment;
        var config = Plugin.SSGIConfig;
        var proj = env.Matrices.Projection;
        var values = new float[24];
        values[0] = (float)(MyRender11.ResolutionF.Y / (Math.Tan(env.Matrices.FovH * 0.5) * 2) * 0.5);
        values[1] = config.Enabled ? MathHelper.Clamp(config.GIIntensity * 2f, 0, 1000) : 0;
        values[2] = 1.5f;
        values[3] = MathHelper.Clamp(config.SliceCount, 0, 1000);
        values[4] = MathHelper.Clamp(config.StepCount, 0, 1000);
        values[5] = MathHelper.Clamp(config.Radius, 0, 1000);
        values[6] = MathHelper.Clamp(config.ExpFactor, 0, 1000);
        values[7] = MathHelper.Clamp(config.Thickness, 0, 1000);
        values[8] = MathHelper.Clamp(config.InputMipLevel, 0, 1000);
        values[9] = 1f;
        values[10] = MyScene.FrameCounter;
        values[11] = Rand.NextUInt();
        values[12] = MyRender11.ResolutionF.X;
        values[13] = MyRender11.ResolutionF.Y;
        values[14] = proj.M11;
        values[15] = proj.M22;
        values[16] = proj.M31;
        values[17] = proj.M32;
        values[18] = proj.M33;
        values[19] = proj.M34;
        values[20] = env.Matrices.FarClipping;
        AnomalyHook.SetUniforms(AnomalyHook.TraceProgramId, values);
    }

    static void UpdateDenoiserCb(MyRenderContext rc, int atrousStepSize, bool updatePrevMatrices)
    {
        using (var mapping = _cbv.MapWriteDiscard(rc))
        {
            var env = MyRender11.Environment;
            var config = Plugin.SSGIConfig;
            var data = new DenoiserCb
            {
                ViewMatrix = env.Matrices.ViewAt0,
                PrevViewMatrix = _prevViewMatrix,
                ScreenSize = MyRender11.ResolutionF,
                MaxHistory = MathHelper.Clamp(config.DenoiserMaxHistory, 0, 1000),
                AtrousStepSize = atrousStepSize,
                Farplane = env.Matrices.FarClipping,
            };
            mapping.Write(in data);
        }

        if (updatePrevMatrices)
            _prevViewMatrix = MyRender11.Environment.Matrices.ViewAt0;
    }

    static void BindCommon(MyRenderContext rc)
    {
        var linearDepth = AnomalyHook.TryGetSrv("linearDepth");
        rc.SetRasterizerState(MyRasterizerStateManager.NocullRasterizerState);
        rc.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);
        rc.PixelShader.SetSamplers(0, MySamplerStateManager.StandardSamplers);
        rc.PixelShader.SetConstantBuffer(0, _cbv);
        rc.PixelShader.SetSrv(0, MyGBuffer.Main.GBuffer0);
        rc.PixelShader.SetSrv(1, MyGBuffer.Main.GBuffer1);
        rc.PixelShader.SetSrv(2, MyGBuffer.Main.GBuffer2);
        rc.PixelShader.SetSrv(3, null);
        rc.PixelShader.SetSrv(4, linearDepth);
    }

    static void DenoiseVarianceGuided(MyRenderContext rc, IRtvTexture input, IRtvTexture history, IRtvTexture output)
    {
        IBorrowedRtvTexture tempRtv = MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI.TempRtvDenoiser",
            _size.X, _size.Y, Format.R16G16B16A16_Float);
        var velocity = AnomalyHook.TryGetSrv("velocity");

        {
            IBorrowedRtvTexture tempRtvMomentsAndHistoryLength =
                MyManagers.RwTexturesPool.BorrowRtv("Prism.SSGI.TempRtvDenoiserVariance",
                    _size.X, _size.Y, _prevMomentsAndHistoryLength.Format);

            rc.SetBlendState(null);
            rc.PixelShader.Set(_psSvgfTemporal);
            rc.PixelShader.SetSrv(5, history);
            rc.PixelShader.SetSrv(6, input);
            rc.PixelShader.SetSrv(7, velocity);
            rc.PixelShader.SetSrv(8, _prevDepthTex);
            rc.PixelShader.SetSrv(9, _prevGBuffer1);
            rc.PixelShader.SetSrv(10, _prevMomentsAndHistoryLength);
            rc.SetRtvs(new[] { tempRtv.Rtv, tempRtvMomentsAndHistoryLength.Rtv });
            MyScreenPass.DrawFullscreenQuad(rc);
            rc.SetRtvNull();
            rc.PixelShader.SetSrv(10, null);

            rc.CopyResource(tempRtvMomentsAndHistoryLength, _prevMomentsAndHistoryLength);
            tempRtvMomentsAndHistoryLength.Release();
        }

        int iterations = Plugin.SSGIConfig.DenoiserBlurIterations;
        if (iterations == 0)
        {
            CompositeAdditive(rc, tempRtv, history, output);
        }
        else
        {
            IRtvTexture atrousInput = tempRtv;
            IRtvTexture atrousOutput = input;

            for (int i = 0; i < iterations; i++)
            {
                UpdateDenoiserCb(rc, (1 << (iterations - 1)) >> i, false);
                rc.PixelShader.SetConstantBuffer(0, _cbv);
                rc.SetBlendState(MyBlendStateManager.BlendReplace);
                rc.PixelShader.Set(_psSvgfAtrous);
                rc.PixelShader.SetSrv(5, atrousInput);
                rc.SetRtv(atrousOutput);
                MyScreenPass.DrawFullscreenQuad(rc);
                rc.SetRtvNull();

                if (i == iterations - 1)
                    CompositeAdditive(rc, atrousOutput, history, output);

                Swap(ref atrousInput, ref atrousOutput);
            }
        }

        tempRtv.Release();
    }

    static void CompositeAdditive(MyRenderContext rc, IRtvTexture filtered, IRtvTexture history, IRtvTexture output)
    {
        rc.CopyResource(filtered, history);
        rc.SetBlendState(MyBlendStateManager.BlendAdditive);
        rc.PixelShader.Set(_psCopyBlend);
        rc.PixelShader.SetSrv(5, history);
        rc.SetRtv(output);
        MyScreenPass.DrawFullscreenQuad(rc);
        rc.SetRtvNull();
        rc.PixelShader.SetSrv(5, null);
    }

    static void Swap<T>(ref T a, ref T b)
    {
        (a, b) = (b, a);
    }

    static void Unbind(MyRenderContext rc)
    {
        rc.SetRtvNull();
        for (var i = 0; i <= 10; i++)
            rc.PixelShader.SetSrv(i, null);
        rc.PixelShader.SetConstantBuffer(0, null);
        rc.PixelShader.Set(null);
        rc.SetBlendState(null);
    }

    static void CopyReplace(MyRenderContext rc, ISrvBindable source, IRtvBindable destination,
        MyViewport? viewport = null, bool shouldStretch = false)
    {
        rc.SetBlendState(null);
        rc.SetInputLayout(null);
        if (source.Size != destination.Size || shouldStretch)
        {
            if (shouldStretch)
                rc.PixelShader.Set(MyCopyToRT.m_stretchPs);
            else
            {
                rc.PixelShader.Set(MyCopyToRT.m_copyFilterPs);
                rc.PixelShader.SetSampler(2, MySamplerStateManager.Linear);
            }
        }
        else
        {
            rc.PixelShader.Set(MyCopyToRT.m_copyPs);
        }

        rc.SetRtv(destination);
        rc.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);
        rc.PixelShader.SetSrv(0, source);
        MyScreenPass.DrawFullscreenQuad(rc, viewport ?? new MyViewport(destination.Size.X, destination.Size.Y));
        rc.PixelShader.SetSrv(0, null);
        rc.SetRtvNull();
    }
}
