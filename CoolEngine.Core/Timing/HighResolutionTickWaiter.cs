using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace CoolEngine.Core.Timing;

/// <summary>
/// The Windows waiter: a high-resolution waitable timer (<c>CREATE_WAITABLE_TIMER_HIGH_RESOLUTION</c>) and a wake
/// event, waited on together. Its accuracy (about 0.3 ms mean) does not depend on any process's timer-resolution
/// request. Never constructed by a browser host.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HighResolutionTickWaiter : ITickWaiter
{
    private const uint CreateWaitableTimerHighResolution = 0x2;
    private const uint TimerAllAccess = 0x1F0003;
    private const uint Infinite = 0xFFFFFFFF;
    private const uint WaitObject0 = 0;
    private const long HundredNanosecondsPerSecond = 10_000_000;

    private readonly ITimestampSource m_time;
    private readonly SafeWaitHandle m_timer;

    // Auto-reset: a Wake that comes while nobody waits stays set until the next wait consumes it.
    private readonly AutoResetEvent m_wake;

    // { timer, wake event }: index 0 is the timer, index 1 the wake. Read by WaitUntil on the loop thread only.
    private readonly IntPtr[] m_handles;

    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="time"/>'s frequency is below 1.</exception>
    /// <exception cref="PlatformNotSupportedException">Not Windows.</exception>
    /// <exception cref="Win32Exception">The timer cannot be created.</exception>
    public HighResolutionTickWaiter(ITimestampSource time)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThan(time.Frequency, 1L);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("HighResolutionTickWaiter needs Windows.");

        // Created only after the checks, so a constructor that throws above leaves no event to dispose.
        m_wake = new AutoResetEvent(false);

        var timer = CreateWaitableTimerExW(IntPtr.Zero, IntPtr.Zero, CreateWaitableTimerHighResolution, TimerAllAccess);
        if (timer == IntPtr.Zero)
        {
            var error = Marshal.GetLastPInvokeError();
            m_wake.Dispose();
            throw new Win32Exception(error);
        }

        m_time = time;
        m_timer = new SafeWaitHandle(timer, ownsHandle: true);
        m_handles = [timer, m_wake.SafeWaitHandle.DangerousGetHandle()];
    }

    /// <inheritdoc />
    /// <remarks>Must not run concurrently with <see cref="Dispose"/>; the host disposes the waiter after the loop
    /// ended.</remarks>
    /// <exception cref="Win32Exception">Setting or waiting on the timer failed.</exception>
    public void WaitUntil(long timestamp)
    {
        while (true)
        {
            var now = m_time.GetTimestamp();
            if (now >= timestamp)
                return;

            // A relative due time is negative, in 100 ns units, rounded up so the timer never fires early.
            var dueTime = -ToHundredNanoseconds((Int128)timestamp - now);
            if (SetWaitableTimer(m_handles[0], ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, 0) == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());

            var result = WaitForMultipleObjects(2, m_handles, 0, Infinite);
            if (result == WaitObject0 + 1)
                return;
            if (result != WaitObject0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    /// <inheritdoc />
    public void Wake()
    {
        m_wake.Set();
    }

    /// <summary>
    /// Closes the timer and the wake event.
    /// </summary>
    public void Dispose()
    {
        m_timer.Dispose();
        m_wake.Dispose();
    }

    // ceil(remaining / Frequency · 10^7), at least 1 and at most long.MaxValue.
    private long ToHundredNanoseconds(Int128 remaining)
    {
        var frequency = (Int128)m_time.Frequency;
        var units = (remaining * HundredNanosecondsPerSecond + frequency - 1) / frequency;
        return units > long.MaxValue ? long.MaxValue : (long)units;
    }

    // Blittable signatures only (handles as IntPtr, BOOL as int), so no unsafe code and no marshalling stubs beyond
    // pinning; LibraryImport would need AllowUnsafeBlocks.
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr lpTimerAttributes, IntPtr lpTimerName, uint dwFlags,
        uint dwDesiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int SetWaitableTimer(IntPtr hTimer, ref long lpDueTime, int lPeriod, IntPtr pfnCompletionRoutine,
        IntPtr lpArgToCompletionRoutine, int fResume);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForMultipleObjects(uint nCount, IntPtr[] lpHandles, int bWaitAll, uint dwMilliseconds);
}
