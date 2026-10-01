import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import { fileURLToPath } from 'node:url';
const client = new Client({ name: 'fm26-live-check', version: '0.1.0' });
try {
  await client.connect(new StdioClientTransport({ command: process.execPath,
    args: [fileURLToPath(new URL('../server/index.mjs', import.meta.url))], stderr: 'inherit' }));
  const tools = await client.listTools();
  const status = await client.callTool({ name: 'fm_get_status', arguments: {} });
  console.log(JSON.stringify({ tools: tools.tools.map(tool => tool.name), status }, null, 2));
  if (status.isError) process.exitCode = 1;
  else {
    const screen = await client.callTool({ name: 'fm_get_screen', arguments: { limit: 100 } });
    console.log(JSON.stringify({ screen }, null, 2));
    if (screen.isError) process.exitCode = 1;
  }
} finally { await client.close(); }
