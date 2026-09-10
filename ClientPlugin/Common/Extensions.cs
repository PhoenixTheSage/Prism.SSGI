using System;
using SharpDX.Direct3D11;
using VRage.Render11.RenderContext;
using VRage.Render11.Resources;

namespace ClientPlugin.Common;

public static class Extensions
{
    public static BufferMapping MapWriteDiscard(this IBuffer buffer)
    {
        return new BufferMapping(buffer.Buffer, MapMode.WriteDiscard);
    }

    public static BufferMapping MapWriteDiscard(this IBuffer buffer, MyRenderContext rc)
    {
        return new BufferMapping(rc.DeviceContext, buffer.Buffer, MapMode.WriteDiscard);
    }

    public static uint NextUInt(this Random rng)
    {
        return (uint)rng.Next(1 << 16) << 16 | (uint)rng.Next(1 << 16);
    }
}
