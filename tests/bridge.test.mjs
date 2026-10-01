import { test } from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { randomUUID } from 'node:crypto';
import { requestBridge, filterScreen } from '../server/bridge.mjs';

async function mock(t, handler) {
  const pipePath = '\\\\.\\pipe\\fm26-test-' + randomUUID();
  const sockets = new Set();
  const server = net.createServer(socket => {
    sockets.add(socket);
    socket.once('close', () => sockets.delete(socket));
    let input = '';
    socket.on('data', chunk => {
      input += chunk.toString();
      if (input.includes('\n')) handler(socket, JSON.parse(input.split('\n')[0]));
    });
  });
  await new Promise(resolve => server.listen(pipePath, resolve));
  t.after(async () => {
    for (const socket of sockets) socket.destroy();
    await new Promise(resolve => server.close(resolve));
  });
  return pipePath;
}

test('pipe transport reconstructs fragmented UTF-8 JSON and sends exact action arguments', async t => {
  const args = { snapshot_id: 'one', element_id: 'eabc', text: 'München ⚽' };
  const pipePath = await mock(t, (socket, request) => {
    assert.deepEqual(request, { command: 'set_text', args });
    const reply = Buffer.from(JSON.stringify({ ok: true, data: { value: args.text } }) + '\n');
    const split = reply.indexOf(Buffer.from('ü')) + 1;
    socket.write(reply.subarray(0, split));
    setTimeout(() => socket.end(reply.subarray(split)), 5);
  });
  assert.deepEqual(await requestBridge('set_text', args, { pipePath }), { value: args.text });
});

test('bridge rejections retain actionable error codes', async t => {
  const pipePath = await mock(t, socket => socket.end(JSON.stringify({ ok: false, error: { code: 'screen_changed', message: 'Observe again.' } }) + '\n'));
  await assert.rejects(requestBridge('click', {}, { pipePath }), { code: 'screen_changed', message: 'Observe again.' });
});

test('a timeout does not retry a potentially executed action', async t => {
  let calls = 0;
  const pipePath = await mock(t, () => { calls++; });
  await assert.rejects(requestBridge('click', {}, { pipePath, timeoutMs: 50 }), { code: 'transport_timeout' });
  assert.equal(calls, 1);
});

test('missing game pipe produces an offline diagnostic', async () => {
  await assert.rejects(requestBridge('status', {}, { pipePath: '\\\\.\\pipe\\absent-' + randomUUID() }), { code: 'game_offline' });
});

test('malformed response fails clearly', async t => {
  const pipePath = await mock(t, socket => socket.end('not json\n'));
  await assert.rejects(requestBridge('status', {}, { pipePath }), { code: 'invalid_response' });
});

test('screen filtering preserves snapshot identity and reports omitted matches', () => {
  const screen = { snapshot_id: 'snapshot-1', warnings: [], nodes: [
    { id: 'e1', text: 'Squad', classes: ['nav'] },
    { id: 'e2', name: 'squad-row', text: 'Player' },
    { id: 'e3', text: 'Inbox' },
  ] };
  const result = filterScreen(screen, { query: 'SQUAD', limit: 1 });
  assert.equal(result.snapshot_id, screen.snapshot_id);
  assert.equal(result.matching_nodes, 2);
  assert.equal(result.total_nodes, 3);
  assert.equal(result.response_truncated, true);
  assert.equal(result.nodes[0].id, 'e1');
});
