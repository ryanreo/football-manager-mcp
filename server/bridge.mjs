import net from 'node:net';

export const PIPE_PATH = process.env.FM26_MCP_PIPE ?? '\\\\.\\pipe\\fm26-mcp';
export class BridgeError extends Error {
  constructor(code, message) { super(message); this.code = code; }
}

/** One request per pipe connection. Never retry a mutation automatically. */
export function requestBridge(command, args = {}, { pipePath = PIPE_PATH, timeoutMs = 18000 } = {}) {
  return new Promise((resolve, reject) => {
    const socket = net.createConnection(pipePath);
    const chunks = [];
    let bytes = 0;
    let done = false;
    const finish = (error, value) => {
      if (done) return;
      done = true;
      clearTimeout(timer);
      socket.destroy();
      error ? reject(error) : resolve(value);
    };
    const timer = setTimeout(() => finish(new BridgeError('transport_timeout',
      'Bridge response timed out. An action may have executed; inspect the screen before retrying.')), timeoutMs);
    socket.once('connect', () => socket.write(JSON.stringify({ command, args }) + '\n'));
    socket.on('data', chunk => {
      bytes += chunk.length;
      if (bytes > 16 * 1024 * 1024) return finish(new BridgeError('response_too_large', 'Bridge response exceeds 16 MiB.'));
      chunks.push(chunk);
      if (!chunk.includes(10)) return;
      try {
        const line = Buffer.concat(chunks).toString('utf8').split('\n')[0];
        const response = JSON.parse(line);
        if (response.ok !== true) return finish(new BridgeError(response.error?.code ?? 'bridge_error', response.error?.message ?? 'Bridge rejected the request.'));
        finish(null, response.data);
      } catch (error) { finish(new BridgeError('invalid_response', error.message)); }
    });
    socket.once('error', error => finish(new BridgeError(
      ['ENOENT', 'ECONNREFUSED'].includes(error.code) ? 'game_offline' : error.code === 'EPERM' || error.code === 'EACCES' ? 'pipe_access_denied' : 'transport_error',
      ['ENOENT', 'ECONNREFUSED'].includes(error.code)
        ? 'FM26 bridge is offline. Start Football Manager after installing the plugin; wait for its menu to load.'
        : ['EPERM', 'EACCES'].includes(error.code)
          ? 'Windows denied access to the game bridge pipe. If FM26 is running elevated, install bridge 0.1.1 or later and restart the game to load its user-SID pipe permissions.'
          : error.message)));
    socket.once('end', () => finish(new BridgeError('connection_closed', 'Bridge closed before returning a result. Inspect the screen before retrying an action.')));
  });
}

export function filterScreen(screen, { query = '', limit = 350 } = {}) {
  const search = query.toLowerCase();
  const nodes = screen.nodes ?? [];
  const matching = search ? nodes.filter(node =>
    [node.name, node.text, node.tooltip, node.type, ...(node.classes ?? [])].some(value => String(value ?? '').toLowerCase().includes(search))) : nodes;
  return { ...screen, nodes: matching.slice(0, limit), total_nodes: nodes.length,
    matching_nodes: matching.length, returned_nodes: Math.min(matching.length, limit),
    response_truncated: matching.length > limit };
}
