param([int]$TargetProcessId)
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
public static class FmTokenInfo {
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError=true)] static extern bool GetTokenInformation(IntPtr token, int kind, IntPtr buffer, int length, out int required);
    static string ReadSid(IntPtr token, int kind) {
        int length;
        GetTokenInformation(token, kind, IntPtr.Zero, 0, out length);
        var buffer = Marshal.AllocHGlobal(length);
        try {
            if(!GetTokenInformation(token, kind, buffer, length, out length)) throw new System.ComponentModel.Win32Exception();
            return new SecurityIdentifier(Marshal.ReadIntPtr(buffer)).Value;
        } finally { Marshal.FreeHGlobal(buffer); }
    }
    public static string[] Read(int pid) {
        var process = OpenProcess(0x1000, false, pid);
        if(process == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        IntPtr token = IntPtr.Zero;
        try {
            if(!OpenProcessToken(process, 8, out token)) throw new System.ComponentModel.Win32Exception();
            return new[] { ReadSid(token, 1), ReadSid(token, 4), ReadSid(token, 25) };
        } finally { if(token != IntPtr.Zero) CloseHandle(token); CloseHandle(process); }
    }
}
'@
$taskResult = [FmTokenInfo]::Read($TargetProcessId)
[pscustomobject]@{ProcessId=$TargetProcessId; UserSid=$taskResult[0]; OwnerSid=$taskResult[1]; IntegritySid=$taskResult[2]} | ConvertTo-Json
