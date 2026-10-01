import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { requestBridge } from '../server/bridge.mjs';
const root = fileURLToPath(new URL('..', import.meta.url));
const game = process.env.FM26_GAME_PATH ?? 'C:\\Games\\Football Manager 26';
const report = {
  node: process.version, platform: process.platform,
  gamePath: game, gameInstalled: existsSync(path.join(game, 'fm.exe')),
  pluginInstalled: existsSync(path.join(game, 'BepInEx', 'plugins', 'FM26McpBridge', 'FM26McpBridge.dll')),
  serverPresent: existsSync(path.join(root, 'server', 'index.mjs')),
};
try { report.bridge = await requestBridge('status'); report.connected = true; }
catch (error) { report.connected = false; report.bridgeError = { code: error.code, message: error.message }; }
console.log(JSON.stringify(report, null, 2));
if (!report.gameInstalled || !report.pluginInstalled) process.exitCode = 1;
