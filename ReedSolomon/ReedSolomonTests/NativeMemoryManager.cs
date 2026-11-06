using System.Buffers;
using System.Runtime.InteropServices;

namespace ReedSolomonTests;

/// <summary>Memory outside the managed heap, so its <see cref="Memory{T}"/> ranges are not backed by an array.</summary>
internal sealed unsafe class NativeMemoryManager : MemoryManager<byte>
{
    private readonly byte* buffer;
    private readonly int length;
    private bool isDisposed;

    internal NativeMemoryManager(int length)
    {
        this.length = length;
        buffer = (byte*)NativeMemory.AllocZeroed((nuint)length);
    }

    public override Span<byte> GetSpan()
    {
        if (isDisposed)
        {
            throw new ObjectDisposedException(nameof(NativeMemoryManager));
        }

        return new Span<byte>(buffer, length);
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        // Native memory never moves, so pinning only reports the address.
        return new MemoryHandle(buffer + elementIndex);
    }

    public override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (isDisposed)
        {
            return;
        }

        NativeMemory.Free(buffer);
        isDisposed = true;
    }
}
