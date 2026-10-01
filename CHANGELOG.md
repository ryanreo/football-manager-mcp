# Changelog

## 0.1.2

- Validate the selected control and its context instead of fingerprinting the full screen. Portal carousel animation no longer rejects otherwise unchanged navigation controls.
- Reject actions after changes to the target, its label, ancestors, open panels, or scene, and reject incomplete screen captures.
- Add seven regression cases covering animation tolerance and stale-control rejection.
- Verify Squad, First Team and Tactics navigation in a loaded FM26 save. Player dropdown support remains incomplete.

## 0.1.1

- Restrict the Windows named pipe to the actual user's SID with a pipe-specific medium integrity label, allowing the ordinary MCP client to connect to the elevated game.
- Verify connection and reversible Preferences navigation in FM26 26.3.0.

## 0.1.0

- Introduce the local BepInEx IL2CPP plugin and MCP tools for rendered screens, clicks, text fields, scrolling, status and diagnostics.
