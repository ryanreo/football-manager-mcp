namespace FM26McpBridge;

// Compare the intended control and its context rather than unrelated animation.
// Kept independent of Unity so the actual action validation can be regression tested.
internal static class SnapshotGuard
{
    internal static string? Validate(string observedScene, IReadOnlyList<Node> observed,
        string currentScene, IReadOnlyList<Node> current, string id)
    {
        if (observedScene != currentScene) return "The game scene changed.";
        var before = observed.ToDictionary(n => n.id);
        var after = current.ToDictionary(n => n.id);
        if (!before.TryGetValue(id, out var oldTarget) || !after.TryGetValue(id, out var target))
            return "The selected control is no longer available.";
        if (PanelSignature(observed) != PanelSignature(current)) return "An open panel changed.";
        if (oldTarget != target && (!SameIdentity(oldTarget, target) || oldTarget.x != target.x || oldTarget.y != target.y
            || oldTarget.width != target.width || oldTarget.height != target.height))
            return "The selected control changed or moved.";
        if (!target.visible || !target.enabled) return "The selected control is hidden or disabled.";
        var parent = oldTarget.parent_id;
        var visited = new HashSet<string>();
        while (parent != null && visited.Add(parent))
        {
            if (!before.TryGetValue(parent, out var oldParent) || !after.TryGetValue(parent, out var newParent)
                || !SameIdentity(oldParent, newParent)) return "The selected control's context changed.";
            parent = oldParent.parent_id;
        }
        if (ChildSignature(observed, id) != ChildSignature(current, id))
            return "The selected control's content changed.";
        return null;
    }
    private static bool SameIdentity(Node a, Node b) => a.id == b.id && a.parent_id == b.parent_id
        && a.type == b.type && a.name == b.name && a.text == b.text && a.visible == b.visible
        && a.enabled == b.enabled && Classes(a) == Classes(b);
    private static string Classes(Node n) => string.Join("|", n.classes.Where(c =>
        !c.Contains("hover", StringComparison.OrdinalIgnoreCase) && !c.Contains("focus", StringComparison.OrdinalIgnoreCase))
        .OrderBy(c => c, StringComparer.Ordinal));
    private static string PanelSignature(IReadOnlyList<Node> nodes) => string.Join("\n", nodes
        .Where(n => n.type == "SI.Bindable.Panel" && n.visible && n.classes.Contains("panel--visible"))
        .OrderBy(n => n.id, StringComparer.Ordinal).Select(n => n.id + ":" + n.name));
    private static string ChildSignature(IReadOnlyList<Node> nodes, string root)
    {
        var children = nodes.Where(n => n.parent_id != null).GroupBy(n => n.parent_id!)
            .ToDictionary(g => g.Key, g => g.ToArray());
        var queue = new Queue<string>();
        var visited = new HashSet<string>();
        var values = new List<string>();
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!visited.Add(id) || !children.TryGetValue(id, out var descendants)) continue;
            foreach (var child in descendants)
            {
                values.Add(child.id + ":" + child.type + ":" + child.name + ":" + child.text
                    + ":" + child.enabled + ":" + child.visible + ":" + Classes(child));
                queue.Enqueue(child.id);
            }
        }
        return string.Join("\n", values.OrderBy(x => x, StringComparer.Ordinal));
    }
}
internal sealed record Node(string id, string? parent_id, string type, string name, string text, string tooltip,
    string[] classes, bool visible, bool enabled, float x, float y, float width, float height);
