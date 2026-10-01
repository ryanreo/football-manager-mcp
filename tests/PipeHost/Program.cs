using FM26McpBridge;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

// Exercise the plugin's actual factory without loading Unity or changing FM.
using var identity = WindowsIdentity.GetCurrent();
var security = PipeFactory.CreateSecurity();
var descriptor = security.GetSecurityDescriptorSddlForm(AccessControlSections.All);
var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier));
if (security.GetOwner(typeof(SecurityIdentifier))?.Value != identity.User!.Value || rules.Count != 1)
    throw new Exception("Pipe permissions must identify only the actual Windows user.");
if ((rules[0] as PipeAccessRule)?.IdentityReference.Value != identity.User.Value)
    throw new Exception("Pipe rule incorrectly uses the process owner.");
using var pipe = PipeFactory.Create(args[0]);
descriptor = PipeFactory.GetDescriptor(pipe);
if (!descriptor.Contains("(ML;;NW;;;ME)")) throw new Exception("Expected medium integrity pipe label: " + descriptor);
Console.WriteLine(JsonSerializer.Serialize(new { ready = true, descriptor, user = identity.User.Value }));
await pipe.WaitForConnectionAsync();
using var reader = new StreamReader(pipe);
using var writer = new StreamWriter(pipe) { AutoFlush = true };
var request = await reader.ReadLineAsync();
await writer.WriteLineAsync(JsonSerializer.Serialize(new { ok = true, data = new { actualFactory = true, request } }));
