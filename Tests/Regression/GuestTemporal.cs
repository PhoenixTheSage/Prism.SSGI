using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.D3DCompiler;
using Buffer=SharpDX.Direct3D11.Buffer;
using Device=SharpDX.Direct3D11.Device;
using Vector4=System.Numerics.Vector4;

static partial class ShaderRegressions
{
    static void GuestTemporal(Device device,string root)
    {
        string pack=Path.Combine(root,"Assets/AnomalyPack/Fullscreen/AfterLighting");
        string anomaly=File.ReadAllText(Path.Combine(root,"../Anomaly/Assets/Shaders/AnomalyFullscreen.hlsli"));
        string preamble=@"
cbuffer Settings:register(b0){row_major float4x4 AnomalyCameraToWorld;float4 AnomalyPassUniform0;}
static const float2 AnomalyPassSize=float2(3,3),AnomalySceneSize=float2(3,3);
static const float4 AnomalyCameraToWorldPad=float4(10000,0,0,0);
uint2 AnomalyScenePixel(float2 uv){return min(uint2(uv*3),uint2(2,2));}
Texture2D<float> AnomalyLinearDepth:register(t1);
Texture2D<float2> AnomalyVelocityBuffer:register(t2);
Texture2D<float4> AnomalyGBuffer1:register(t5);
Texture2D<float4> AnomalyPackSrv0:register(t7),AnomalyPackSrv1:register(t8),AnomalyPackSrv2:register(t9),AnomalyPackSrv3:register(t12),AnomalyPackSrv4:register(t13);
RWStructuredBuffer<float4> Output:register(u0);
";
        string helpers=Function(anomaly,"AnomalyIsForeground")+Function(anomaly,"AnomalyViewToWorldAt0")+File.ReadAllText(Path.Combine(pack,"Denoise.hlsli"));
        foreach(string file in new[]{"Temporal.hlsl","Moments.hlsl"})
        {
            string source=preamble+helpers+Function(File.ReadAllText(Path.Combine(pack,file)),"__pixel_shader")+
                "[numthreads(1,1,1)]void main(uint3 id:SV_DispatchThreadID){Output[id.y*3+id.x]=__pixel_shader(float4(id.xy+.5,0,1),(id.xy+.5)/3);}";
            using var bytecode=ShaderBytecode.Compile(source,"main","cs_5_0",ShaderFlags.OptimizationLevel3);
            using var shader=new ComputeShader(device,bytecode);
            foreach(string scenario in new[]{"identity","rotation","normal rejection","first frame","zero cap","long cap"})
            {
                var constants=new float[20];foreach(int i in new[]{0,5,10,15})constants[i]=1;
                if(scenario=="rotation"){constants[0]=constants[10]=0;constants[2]=-1;constants[8]=1;}
                constants[16]=scenario=="zero cap"?0:scenario=="long cap"?64:16;
                using var cb=Buffer.Create(device,BindFlags.ConstantBuffer,constants);
                using var normals=Texture(device,Enumerable.Repeat(new Vector4(.5f,.5f,0,0),9).ToArray());
                using var previousNormal=Texture(device,Enumerable.Repeat(scenario=="rotation"?new Vector4(1,0,0,1):new Vector4(0,0,scenario=="normal rejection"?-1:1,1),9).ToArray());
                using var depth=Texture(device,Enumerable.Repeat(new Vector4(5,0,0,0),9).ToArray());
                using var history=Texture(device,new Vector4[9]);
                using var trace=Texture(device,Enumerable.Repeat(new Vector4(1,1,1,0),9).ToArray());
                using var moments=Texture(device,Enumerable.Repeat(new Vector4(0,0,32,0),9).ToArray());
                using var ns=new ShaderResourceView(device,normals);using var pn=new ShaderResourceView(device,previousNormal);
                using var ds=new ShaderResourceView(device,depth);using var hs=new ShaderResourceView(device,history);
                using var ts=new ShaderResourceView(device,trace);using var ms=new ShaderResourceView(device,moments);
                using var output=new Buffer(device,new BufferDescription{SizeInBytes=144,StructureByteStride=16,BindFlags=BindFlags.UnorderedAccess,OptionFlags=ResourceOptionFlags.BufferStructured,Usage=ResourceUsage.Default});
                using var uav=new UnorderedAccessView(device,output);
                using var staging=new Buffer(device,new BufferDescription{SizeInBytes=144,CpuAccessFlags=CpuAccessFlags.Read,Usage=ResourceUsage.Staging});
                var context=device.ImmediateContext;context.ComputeShader.Set(shader);context.ComputeShader.SetConstantBuffer(0,cb);
                context.ComputeShader.SetShaderResource(1,ds);context.ComputeShader.SetShaderResource(5,ns);
                context.ComputeShader.SetShaderResource(7,hs);context.ComputeShader.SetShaderResource(8,ts);
                context.ComputeShader.SetShaderResource(9,scenario=="first frame"?null:ds);context.ComputeShader.SetShaderResource(12,pn);context.ComputeShader.SetShaderResource(13,ms);
                context.ComputeShader.SetUnorderedAccessView(0,uav);context.Dispatch(3,3,1);context.ComputeShader.SetUnorderedAccessView(0,null);context.CopyResource(output,staging);
                context.MapSubresource(staging,MapMode.Read,MapFlags.None,out DataStream stream);
                try
                {
                    stream.Position=64;var actual=stream.Read<Vector4>();
                    float age=scenario=="normal rejection"||scenario=="first frame"?1:Math.Min(32,constants[16])+1;
                    if(Math.Abs(actual.X-1/age)>1e-4 || file=="Moments.hlsl" && Math.Abs(actual.Z-age)>1e-4)
                        throw new Exception($"Guest {file} {scenario}: {actual}, expected age {age}");
                }
                finally{context.UnmapSubresource(staging,0);stream.Dispose();for(int i=0;i<14;i++)context.ComputeShader.SetShaderResource(i,null);context.ComputeShader.SetConstantBuffer(0,null);context.ComputeShader.Set(null);}
            }
        }
        Console.WriteLine("PASS: production guest Temporal/Moments on WARP: exact ages, camera rotation, normal rejection, first frame and 0/16/64 history caps.");
    }
}
