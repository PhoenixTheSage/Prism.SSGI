using SharpDX.Direct3D11;
using System;
using Buffer = SharpDX.Direct3D11.Buffer;

namespace ClientPlugin.Common;

public struct BufferMapping : IDisposable
{
    private readonly DeviceContext _context;
    private readonly Buffer _buffer;
    private readonly IntPtr _ptr;
    private readonly int _sizeInBytes;
    private int _offsetInBytes = 0;

    public BufferMapping(Buffer buffer, MapMode mode, MapFlags flags = MapFlags.None)
        : this(buffer.Device.ImmediateContext, buffer, mode, flags)
    {
    }

    public BufferMapping(Device device, Buffer buffer, MapMode mode, MapFlags flags = MapFlags.None)
        : this(device.ImmediateContext, buffer, mode, flags)
    {
    }

    public BufferMapping(DeviceContext context, Buffer buffer, MapMode mode, MapFlags flags = MapFlags.None)
    {
        _context = context;
        _buffer = buffer;
        _ptr = _context.MapSubresource(buffer, 0, mode, flags).DataPointer;
        _sizeInBytes = _buffer.Description.SizeInBytes;
    }

    public readonly unsafe void Write<T>(ref readonly T data, int destOffsetInBytes = 0) where T : unmanaged
    {
        ((T*)(_ptr + _offsetInBytes + destOffsetInBytes))[0] = data;
    }

    public unsafe void WriteAndPosition<T>(ref T data) where T : unmanaged
    {
        ((T*)(_ptr + _offsetInBytes))[0] = data;
        Offset(sizeof(T));
    }

    public readonly unsafe void Write<T>(Span<T> span, int destOffsetInBytes = 0) where T : unmanaged => Write((ReadOnlySpan<T>)span, destOffsetInBytes);
    public readonly unsafe void Write<T>(ReadOnlySpan<T> span, int destOffsetInBytes = 0) where T : unmanaged
    {
        if (span.IsEmpty)
            return;

        long destBytes = _sizeInBytes - _offsetInBytes - destOffsetInBytes;
        fixed (T* ptr = span)
        {
            System.Buffer.MemoryCopy(ptr, (void*)(_ptr + _offsetInBytes + destOffsetInBytes), destBytes, sizeof(T) * span.Length);
        }
    }

    public unsafe void WriteAndPosition<T>(Span<T> span) where T : unmanaged => WriteAndPosition((ReadOnlySpan<T>)span);
    public unsafe void WriteAndPosition<T>(ReadOnlySpan<T> span) where T : unmanaged
    {
        Write(span);
        Offset(sizeof(T) * span.Length);
    }

    public void Offset(int bytes)
    {
        _offsetInBytes += bytes;
    }

    public readonly void Dispose()
    {
        _context.UnmapSubresource(_buffer, 0);
    }
}
