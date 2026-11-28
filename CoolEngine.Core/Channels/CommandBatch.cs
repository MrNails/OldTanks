namespace CoolEngine.Core.Channels;

/// <summary>
/// The commands one <see cref="CommandQueue{T}.TakePending"/> took, in sequence order: command <c>i</c> has the
/// sequence <c>FirstSequence + i</c>. An empty batch has <c>FirstSequence = LastSequence + 1</c>. Valid until the next
/// <see cref="CommandQueue{T}.TakePending"/>.
/// </summary>
public readonly ref struct CommandBatch<T> where T : struct
{
    internal CommandBatch(ReadOnlySpan<T> commands, long firstSequence, long lastSequence)
    {
        Commands = commands;
        FirstSequence = firstSequence;
        LastSequence = lastSequence;
    }

    public ReadOnlySpan<T> Commands { get; }

    public long FirstSequence { get; }

    public long LastSequence { get; }

    public int Count => Commands.Length;
}
