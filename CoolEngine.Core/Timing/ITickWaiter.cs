namespace CoolEngine.Core.Timing;

/// <summary>
/// Paces a tick loop: blocks the loop thread until a timestamp of the waiter's time source, or until woken.
/// </summary>
public interface ITickWaiter : IDisposable
{
    /// <summary>
    /// The loop thread only. Returns at or after <paramref name="timestamp"/> by the waiter's time source, or earlier
    /// after <see cref="Wake"/>. Callers re-check their state: a spurious early return is allowed.
    /// </summary>
    void WaitUntil(long timestamp);

    /// <summary>
    /// Any thread. Makes the current or the next <see cref="WaitUntil"/> return promptly; one pending wake is
    /// remembered.
    /// </summary>
    void Wake();
}
