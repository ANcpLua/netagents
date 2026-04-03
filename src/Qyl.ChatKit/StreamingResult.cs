namespace Qyl.ChatKit;

/// <summary>Wraps an async stream of SSE byte chunks.</summary>
public sealed class StreamingResult(IAsyncEnumerable<byte[]> stream) : IAsyncEnumerable<byte[]>
{
    public IAsyncEnumerator<byte[]> GetAsyncEnumerator(CancellationToken ct = default)
        => stream.GetAsyncEnumerator(ct);
}

/// <summary>Wraps a single JSON byte response.</summary>
public sealed class NonStreamingResult(byte[] json)
{
    public byte[] Json { get; } = json;
}
