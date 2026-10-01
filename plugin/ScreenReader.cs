using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UObj = UnityEngine.Object;

namespace FM26McpBridge;

internal sealed class ScreenReader
{
    private readonly string session = Guid.NewGuid().ToString("N");
    private Snapshot? last;
    public object Execute(JsonElement request)
    {
        if (request.ValueKind != JsonValueKind.Object || !request.TryGetProperty("command", out var command))
            return Replies.Error("invalid_request", "Missing command.");
        var args = request.TryGetProperty("args", out var a) ? a : default;
        switch (command.GetString())
        {
            case "status":
                return Replies.Ok(new { version = "0.1.1", session, processId = Process.GetCurrentProcess().Id,
                    unityVersion = Application.unityVersion, scene = SceneManager.GetActiveScene().name,
                    game = "Football Manager 26", capabilities = new[] { "screen", "click", "set_text", "scroll", "diagnostics" } });
            case "screen":
                last = Capture();
                return Replies.Ok(last.Public());
            case "diagnostics":
                return Replies.Ok(new { scene = SceneManager.GetActiveScene().name,
                    documents = UObj.FindObjectsOfType<UIDocument>().Length,
                    assemblies = AppDomain.CurrentDomain.GetAssemblies().Select(x => x.GetName().Name).OrderBy(x => x).ToArray() });
            case "click": case "set_text": case "scroll":
                return Act(command.GetString()!, args);
            default: return Replies.Error("unknown_command", "Unsupported bridge command.");
        }
    }
    private object Act(string command, JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object) return Replies.Error("invalid_request", "Missing action arguments.");
        if (!args.TryGetProperty("snapshot_id", out var sid) || last == null || sid.GetString() != last.Id)
            return Replies.Error("stale_snapshot", "Read the screen and use its snapshot_id before acting.");
        if (DateTime.UtcNow - last.Created > TimeSpan.FromMinutes(2))
            return Replies.Error("stale_snapshot", "Snapshot expired. Read the screen again.");
        var current = Capture();
        if (current.Fingerprint != last.Fingerprint)
            return Replies.Error("screen_changed", "The interface changed. Read a new screen before acting.");
        var id = args.GetProperty("element_id").GetString() ?? "";
        if (!current.Elements.TryGetValue(id, out var target) || target.panel == null || !target.enabledInHierarchy)
            return Replies.Error("element_unavailable", "Element is missing or disabled.");
        var info = current.Nodes.FirstOrDefault(x => x.id == id);
        if (info == null || !info.visible) return Replies.Error("element_unavailable", "Element is not visible.");
        // Each snapshot permits one action. Even a failed dispatch requires a fresh observation.
        last = null;
        if (command == "click")
        {
            var evt = ClickEvent.GetPooled();
            try
            {
            evt.button = 0;
            evt.clickCount = 1;
            evt.pointerId = 0;
            evt.pointerType = "mouse";
            evt.position = new Vector3(info.x + info.width / 2, info.y + info.height / 2, 0);
            evt.target = target.Cast<IEventHandler>();
            var button = target.TryCast<Button>();
            if (button?.clickable != null) button.clickable.SimulateSingleClick(evt, 0);
            else target.SendEvent(evt);
            }
            finally { evt.Dispose(); }
            return Replies.Ok(new { dispatched = true, verified = false, action = command, element_id = id,
                next = "Read the screen to verify the result. Some custom FM controls may require native pointer input." });
        }
        if (command == "set_text")
        {
            var field = target.TryCast<TextField>();
            if (field == null || field.isReadOnly) return Replies.Error("unsupported_element", "Expected an editable TextField.");
            var value = args.GetProperty("text").GetString() ?? "";
            if (value.Length > 4096) return Replies.Error("invalid_request", "Text limit is 4096 characters.");
            field.Focus();
            field.value = value;
            return Replies.Ok(new { dispatched = true, verified = field.value == value, action = command, value = field.value });
        }
        var scroll = target.TryCast<ScrollView>();
        if (scroll == null) return Replies.Error("unsupported_element", "Expected a ScrollView.");
        var dx = args.TryGetProperty("delta_x", out var x) ? x.GetSingle() : 0;
        var dy = args.TryGetProperty("delta_y", out var y) ? y.GetSingle() : 0;
        if (!float.IsFinite(dx) || !float.IsFinite(dy) || Math.Abs(dx) > 5000 || Math.Abs(dy) > 5000)
            return Replies.Error("invalid_request", "Scroll deltas must be finite and within 5000 pixels.");
        scroll.scrollOffset += new Vector2(dx, dy);
        return Replies.Ok(new { dispatched = true, verified = false, action = command,
            offset = new { x = scroll.scrollOffset.x, y = scroll.scrollOffset.y } });
    }
    private Snapshot Capture()
    {
        var result = new Snapshot { Scene = SceneManager.GetActiveScene().name };
        var roots = new HashSet<long>();
        foreach (var document in UObj.FindObjectsOfType<UIDocument>())
        {
            if (document.rootVisualElement == null || document.rootVisualElement.panel == null) continue;
            var root = document.rootVisualElement.panel.visualTree;
            if (!roots.Add(root.Pointer.ToInt64())) continue;
            result.Documents.Add(document.name);
            Walk(root, null, true, result, 0);
        }
        // FM can attach runtime panels without an active UIDocument component.
        var panels = UIElementsRuntimeUtility.GetSortedPlayerPanels();
        for (var i = 0; i < panels.Count; i++)
        {
            var root = panels[i].visualTree;
            if (root == null || !roots.Add(root.Pointer.ToInt64())) continue;
            result.Documents.Add("runtime-panel-" + i);
            Walk(root, null, true, result, 0);
        }
        if (result.Documents.Count == 0) result.Warnings.Add("No active UI panels. Wait for the menu to load; run diagnostics if this persists.");
        var fingerprintInput = result.Scene + JsonSerializer.Serialize(result.Nodes);
        result.Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintInput)));
        result.Id = session + ":" + Guid.NewGuid().ToString("N");
        return result;
    }
    private static void Walk(VisualElement element, string? parent, bool ancestorVisible, Snapshot result, int depth)
    {
        if (depth > 100 || result.Visited++ >= 50000 || result.Nodes.Count >= 5000)
        {
            result.Truncated = true;
            return;
        }
        var style = element.resolvedStyle;
        var bound = element.worldBound;
        var visible = ancestorVisible && style.display != DisplayStyle.None && style.visibility == Visibility.Visible
            && style.opacity > 0 && bound.width > 0 && bound.height > 0;
        var id = "e" + element.Pointer.ToInt64().ToString("x");
        var text = element.TryCast<TextElement>()?.text ?? element.TryCast<TextField>()?.value ?? "";
        var type = element.GetIl2CppType().FullName ?? "VisualElement";
        var classes = new List<string>();
        var cls = element.classList;
        for (var i = 0; i < cls.Count; i++) classes.Add(cls[i]);
        var interesting = visible && (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(element.name)
            || type.Contains("Button") || type.Contains("Field") || type.Contains("ScrollView") || !string.IsNullOrEmpty(element.tooltip));
        if (interesting)
        {
            result.Elements[id] = element;
            result.Nodes.Add(new Node(id, parent, type, element.name ?? "", Limit(text, 2048), Limit(element.tooltip ?? "", 1024),
                classes.ToArray(), visible, element.enabledInHierarchy, Round(bound.x), Round(bound.y), Round(bound.width), Round(bound.height)));
            parent = id;
        }
        var count = element.childCount;
        for (var i = 0; i < count; i++) Walk(element.ElementAt(i), parent, visible, result, depth + 1);
    }
    private static string Limit(string value, int size) => value.Length <= size ? value : value[..size];
    private static float Round(float value) => float.IsFinite(value) ? (float)Math.Round(value, 1) : 0;
}
internal sealed class Snapshot
{
    public string Id = "";
    public string Scene = "";
    public string Fingerprint = "";
    public readonly DateTime Created = DateTime.UtcNow;
    public int Visited;
    public bool Truncated;
    public readonly List<string> Documents = new();
    public readonly List<string> Warnings = new();
    public readonly List<Node> Nodes = new();
    public readonly Dictionary<string, VisualElement> Elements = new();
    public object Public() => new { snapshot_id = Id, scene = Scene, captured_at = Created.ToString("O"),
        documents = Documents, nodes = Nodes, visited = Visited, truncated = Truncated, warnings = Warnings };
}
internal sealed record Node(string id, string? parent_id, string type, string name, string text, string tooltip,
    string[] classes, bool visible, bool enabled, float x, float y, float width, float height);
