import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import assert from 'node:assert/strict';
import { requestBridge } from '../server/bridge.mjs';

const pipeName = 'fm26-acl-test-' + randomUUID();
const host = spawn(fileURLToPath(new URL('../.tools/dotnet/dotnet.exe', import.meta.url)),
  [fileURLToPath(new URL('../tests/PipeHost/bin/Release/net6.0/PipeHost.dll', import.meta.url)), pipeName],
  { windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
let errors = '';
host.stderr.on('data', chunk => { errors += chunk; });
try {
  const ready = await new Promise((resolve, reject) => {
    let output = '';
    const timer = setTimeout(() => reject(new Error('Pipe host startup timed out. ' + errors)), 10000);
    host.once('error', error => { clearTimeout(timer); reject(error); });
    host.once('exit', code => { if (!output.includes('\n')) { clearTimeout(timer); reject(new Error('Pipe host exited: ' + code + ' ' + errors)); } });
    host.stdout.on('data', chunk => {
      output += chunk;
      if (output.includes('\n')) { clearTimeout(timer); resolve(JSON.parse(output.split('\n')[0])); }
    });
  });
  assert.equal(ready.ready, true);
  const reply = await requestBridge('status', {}, { pipePath: '\\\\.\\pipe\\' + pipeName });
  assert.equal(reply.actualFactory, true);
  assert.deepEqual(JSON.parse(reply.request), { command: 'status', args: {} });
  console.log(JSON.stringify({ passed: true, actualPluginPipeFactory: true, userSid: ready.user, descriptor: ready.descriptor }, null, 2));
} finally { host.kill(); }
