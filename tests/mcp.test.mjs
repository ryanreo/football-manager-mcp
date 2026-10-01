import { test } from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { randomUUID } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

test('real MCP stdio handshake, tool discovery, screen reading, dispatch, and schema validation', async t => {
  const pipePath = '\\\\.\\pipe\\fm26-mcp-integration-' + randomUUID();
  const requests = [];
  const sockets = new Set();
  const fakeGame = net.createServer(socket => {
    sockets.add(socket);
    socket.once('close', () => sockets.delete(socket));
    let input = '';
    socket.on('data', chunk => {
      input += chunk.toString();
      if (!input.includes('\n')) return;
      const request = JSON.parse(input.split('\n')[0]);
      requests.push(request);
      const data = request.command === 'screen'
        ? { snapshot_id: 's1', scene: 'menu', nodes: [{ id: 'e123', text: 'Preferences', enabled: true, visible: true }] }
        : request.command === 'click' ? { dispatched: true, verified: false }
        : { version: '0.1.0', session: 'test-session' };
      socket.end(JSON.stringify({ ok: true, data }) + '\n');
    });
  });
  await new Promise(resolve => fakeGame.listen(pipePath, resolve));
  const client = new Client({ name: 'fm26-test-client', version: '1.0.0' });
  const transport = new StdioClientTransport({ command: process.execPath,
    args: [fileURLToPath(new URL('../server/index.mjs', import.meta.url))],
    env: { ...process.env, FM26_MCP_PIPE: pipePath }, stderr: 'pipe' });
  let stderr = '';
  transport.stderr?.on('data', chunk => { stderr += chunk; });
  t.after(async () => {
    await client.close();
    for (const socket of sockets) socket.destroy();
    await new Promise(resolve => fakeGame.close(resolve));
  });
  await client.connect(transport);
  const listed = await client.listTools();
  assert.equal(listed.tools.length, 6);
  assert.ok(listed.tools.some(tool => tool.name === 'fm_get_screen'));
  assert.equal(listed.tools.find(tool => tool.name === 'fm_click').annotations.readOnlyHint, false);
  const screen = await client.callTool({ name: 'fm_get_screen', arguments: { query: 'preferences' } });
  assert.equal(screen.isError, undefined);
  assert.equal(screen.structuredContent.snapshot_id, 's1');
  assert.equal(screen.structuredContent.nodes[0].id, 'e123');
  const click = await client.callTool({ name: 'fm_click', arguments: { snapshot_id: 's1', element_id: 'e123' } });
  assert.equal(click.structuredContent.dispatched, true);
  assert.equal(click.structuredContent.verified, false);
  assert.deepEqual(requests.at(-1), { command: 'click', args: { snapshot_id: 's1', element_id: 'e123' } });
  const count = requests.length;
  const invalid = await client.callTool({ name: 'fm_click', arguments: { element_id: 'e123' } });
  assert.equal(invalid.isError, true);
  assert.equal(requests.length, count);
  assert.equal(stderr, '');
});
