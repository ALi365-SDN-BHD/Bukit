using Scriban.Runtime;

namespace Bukit.Rendering.Scriban;

/// <summary>
/// Marks a converted template value and every nested <see cref="ScriptObject"/> and
/// <see cref="ScriptArray"/> as read-only. Scriban does not propagate IsReadOnly to
/// nested values, so shared values must be walked explicitly.
/// </summary>
internal static class ScriptValueFreezer
{
    internal static T Freeze<T>(T value) where T : class
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(value);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            switch (current)
            {
                case ScriptArray array:
                    foreach (var item in array)
                    {
                        PushContainer(pending, item);
                    }

                    // Create the lazy member store now so concurrent reads never allocate it.
                    _ = array.ScriptObject;
                    array.IsReadOnly = true;
                    break;
                case ScriptObject obj:
                    foreach (var entry in obj)
                    {
                        PushContainer(pending, entry.Value);
                    }

                    obj.IsReadOnly = true;
                    break;
            }
        }

        return value;
    }

    private static void PushContainer(Stack<object> pending, object? value)
    {
        if (value is ScriptArray or ScriptObject)
        {
            pending.Push(value);
        }
    }
}
