using FM26McpBridge;

static Node Make(string id, string? parent, string type, string name, string text = "", float width = 50) =>
    new(id, parent, type, name, text, "", Array.Empty<string>(), true, true, 0, 0, width, 20);
var panel = Make("epanel", null, "SI.Bindable.Panel", "Portal") with { classes = new[] { "panel--visible" } };
var target = Make("ebutton", "epanel", "SI.Bindable.SIButton", "Squad");
var label = Make("elabel", "ebutton", "SI.Bindable.SIText", "label", "Squad");
var carousel = Make("ebar", "epanel", "VisualElement", "ProgressBar", width: 1);
var original = new[] { panel, target, label, carousel };
var animation = new[] { panel, target, label, carousel with { width = 33 } };
void Assert(bool pass, string message) { if (!pass) throw new Exception(message); Console.WriteLine("PASS " + message); }
Assert(SnapshotGuard.Validate("GameWorld", original, "GameWorld", animation, "ebutton") == null,
    "unrelated carousel animation permits the observed control");
Assert(SnapshotGuard.Validate("GameWorld", original, "GameWorld", new[] { panel, target, label with { text = "Quit" }, carousel }, "ebutton") != null,
    "recycled control with changed label is rejected");
Assert(SnapshotGuard.Validate("GameWorld", original, "GameWorld", new[] { panel, target with { x = 99 }, label, carousel }, "ebutton") != null,
    "moved target is rejected");
Assert(SnapshotGuard.Validate("GameWorld", original, "GameWorld", new[] { panel, target with { enabled = false }, label, carousel }, "ebutton") != null,
    "disabled target is rejected");
Assert(SnapshotGuard.Validate("GameWorld", original, "GameWorld", animation.Concat(new[] { panel with { id = "emodal", name = "Confirm Transfer" } }).ToArray(), "ebutton") != null,
    "new modal panel is rejected");
Assert(SnapshotGuard.Validate("GameWorld", original, "MatchScene", animation, "ebutton") != null,
    "changed scene is rejected");
Assert(SnapshotGuard.Validate("GameWorld", original, "GameWorld", animation, "emissing") != null,
    "missing control is rejected");
