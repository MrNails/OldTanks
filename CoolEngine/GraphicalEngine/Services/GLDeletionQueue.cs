using System.Collections.Concurrent;
using OpenTK.Graphics.OpenGL4;

namespace CoolEngine.GraphicalEngine.Services;

/// <summary>
/// The kind of a GL object name queued on <see cref="GLDeletionQueue"/>; it picks the delete call.
/// </summary>
public enum GLObjectKind
{
    Texture,
    Buffer,
    VertexArray,
    Program
}

/// <summary>
/// Carries the GL names of objects that were never disposed from their finalizers to the GL thread, which deletes them.
/// A finalizer runs on the finalizer thread, which has no GL context, so it must not call OpenGL itself (KI-017).
/// </summary>
public static class GLDeletionQueue
{
    private static readonly ConcurrentQueue<(GLObjectKind Kind, int Name)> s_pending = new();
    private static readonly Action<GLObjectKind, int> s_deleteName = DeleteName;

    /// <summary>
    /// The number of names waiting to be deleted (tests and diagnostics).
    /// </summary>
    internal static int PendingCount => s_pending.Count;

    /// <summary>
    /// Queues a GL name for deletion on the GL thread. Callable from any thread, including the finalizer thread:
    /// it makes no GL call, takes no lock of its own (<see cref="ConcurrentQueue{T}"/> locks briefly inside when it grows),
    /// logs nothing and never throws (an exception in a finalizer ends the process).
    /// A name of 0 is ignored. The name stays allocated in GL until <see cref="DeletePending"/> deletes it.
    /// </summary>
    /// <param name="kind">What the name is, which picks the delete call.</param>
    /// <param name="name">The GL name; 0 queues nothing.</param>
    public static void Enqueue(GLObjectKind kind, int name)
    {
        if (name == 0)
            return;

        s_pending.Enqueue((kind, name));
    }

    /// <summary>
    /// Deletes every queued name with the matching GL delete call. GL thread only, and only while the context exists;
    /// called once per update frame and once more when the window unloads. Allocates nothing when the queue is empty.
    /// Logs nothing: each finalizer has already logged its leak.
    /// </summary>
    /// <returns>The number of names deleted.</returns>
    public static int DeletePending()
    {
        return Drain(s_deleteName);
    }

    /// <summary>
    /// Hands every queued name to <paramref name="delete"/>, in queue order, and removes it from the queue.
    /// A name queued while this runs is handed over in this call or the next one, never twice.
    /// </summary>
    /// <param name="delete">Called once per queued name.</param>
    /// <returns>The number of names handed over.</returns>
    internal static int Drain(Action<GLObjectKind, int> delete)
    {
        var count = 0;
        while (s_pending.TryDequeue(out var entry))
        {
            delete(entry.Kind, entry.Name);
            count++;
        }

        return count;
    }

    private static void DeleteName(GLObjectKind kind, int name)
    {
        switch (kind)
        {
            case GLObjectKind.Texture:
                GL.DeleteTexture(name);
                break;
            case GLObjectKind.Buffer:
                GL.DeleteBuffer(name);
                break;
            case GLObjectKind.VertexArray:
                GL.DeleteVertexArray(name);
                break;
            case GLObjectKind.Program:
                GL.DeleteProgram(name);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown GL object kind.");
        }
    }
}
