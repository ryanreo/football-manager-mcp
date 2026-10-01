using BepInEx;
using BepInEx.Unity.IL2CPP;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using UnityEngine;

namespace FM26McpBridge;

[BepInPlugin("local.fm26.mcp.bridge", "FM26 MCP Bridge", "0.1.1")]
[BepInProcess("fm.exe")]
public sealed class Plugin : BasePlugin
{
    private CancellationTokenSource? stop;
    public override void Load()
    {
        stop = new CancellationTokenSource();
        AddComponent<BridgePump>();
        _ = Task.Run(() => PipeLoop(stop.Token));
        Log.LogInfo("FM26 MCP Bridge 0.1.1 loaded. User-SID restricted named pipe: fm26-mcp");
    }
    public override bool Unload()
    {
        stop?.Cancel();
        return true;
    }
    private async Task PipeLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var pipe = PipeFactory.Create();
                await pipe.WaitForConnectionAsync(token);
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                requestTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                var bytes = new List<byte>();
                var one = new byte[1];
                while (bytes.Count < 65536)
                {
                    if (await pipe.ReadAsync(one.AsMemory(), requestTimeout.Token) == 0) break;
                    if (one[0] == 10) break;
                    bytes.Add(one[0]);
                }
                object result;
                if (bytes.Count >= 65536) result = Replies.Error("request_too_large", "Request limit is 64 KiB.");
                else
                {
                    try
                    {
                        using var parsed = JsonDocument.Parse(bytes.ToArray());
                        var job = new Job(parsed.RootElement.Clone());
                        if (BridgeRuntime.Jobs.Count >= 8) result = Replies.Error("busy", "Bridge queue is full.");
                        else
                        {
                            BridgeRuntime.Jobs.Enqueue(job);
                            try { result = await job.Result.Task.WaitAsync(TimeSpan.FromSeconds(12), token); }
                            catch (TimeoutException)
                            {
                                var wasQueued = Interlocked.CompareExchange(ref job.State, 2, 0) == 0;
                                result = Replies.Error(wasQueued ? "game_not_responding" : "action_outcome_unknown",
                                    wasQueued ? "No game update received; request cancelled before execution."
                                    : "Request started but response timed out. Inspect the screen before repeating an action.");
                            }
                        }
                    }
                    catch (JsonException) { result = Replies.Error("invalid_json", "Expected one JSON request."); }
                }
                var response = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result) + "\n");
                using var writeTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                writeTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                await pipe.WriteAsync(response.AsMemory(), writeTimeout.Token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) { Log.LogWarning("MCP pipe: " + ex.Message); }
        }
    }
}

internal sealed class Job
{
    public readonly JsonElement Request;
    public readonly TaskCompletionSource<object> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public readonly DateTime Deadline = DateTime.UtcNow.AddSeconds(11);
    public int State;
    public Job(JsonElement request) { Request = request; }
}
internal static class Replies
{
    public static object Ok(object data) => new { ok = true, data };
    public static object Error(string code, string message) => new { ok = false, error = new { code, message } };
}

public sealed class BridgePump : MonoBehaviour
{
    public BridgePump(IntPtr pointer) : base(pointer) { }
    public void Update() => BridgeRuntime.Tick();
}

internal static class BridgeRuntime
{
    internal static readonly ConcurrentQueue<Job> Jobs = new();
    private static readonly ScreenReader Reader = new();
    public static void Tick()
    {
        if (!Jobs.TryDequeue(out var job)) return;
        if (DateTime.UtcNow > job.Deadline || Interlocked.CompareExchange(ref job.State, 1, 0) != 0)
        {
            job.Result.TrySetResult(Replies.Error("expired", "Request expired before execution."));
            return;
        }
        try { job.Result.TrySetResult(Reader.Execute(job.Request)); }
        catch (Exception ex) { job.Result.TrySetResult(Replies.Error("bridge_error", ex.GetType().Name + ": " + ex.Message)); }
        finally { Interlocked.Exchange(ref job.State, 2); }
    }
}
