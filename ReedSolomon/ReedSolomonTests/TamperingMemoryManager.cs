using System.Buffers;

namespace ReedSolomonTests;

/// <summary>Runs an action the first time its memory is read, imitating a caller that changes shards during a call.</summary>
/// <remarks>
/// The codec checks shard lengths before it reads any shard's memory, so the action runs after that check.
/// This makes the race between validation and use reproducible without a second thread.
/// </remarks>
internal sealed class TamperingMemoryManager : MemoryManager<byte>
{
    private readonly byte[] buffer;
    private readonly Action tamper;
    private bool hasTampered;

    internal TamperingMemoryManager(byte[] buffer, Action tamper)
    {
        this.buffer = buffer;
        this.tamper = tamper;
    }

    /// <summary>The memory, created without reading it. <see cref="MemoryManager{T}.Memory"/> would read it once.</summary>
    internal Memory<byte> UnreadMemory
    {
        get
        {
            return CreateMemory(buffer.Length);
        }
    }

    public override Span<byte> GetSpan()
    {
        if (hasTampered == false)
        {
            hasTampered = true;
            tamper();
        }

        return buffer;
    }

    public override MemoryHandle Pin(int elementIndex = 0)
    {
        throw new NotSupportedException("The codec does not pin shard memory.");
    }

    public override void Unpin()
    {
    }

    protected override void Dispose(bool disposing)
    {
    }
}
