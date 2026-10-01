import { requestBridge, filterScreen } from '../server/bridge.mjs';
const [command = 'status', argument = ''] = process.argv.slice(2);
if (!['status', 'screen', 'diagnostics'].includes(command)) {
  console.error('Usage: node scripts/bridge-cli.mjs status|screen [search text]|diagnostics');
  process.exitCode = 2;
} else {
  try {
    const result = await requestBridge(command);
    console.log(JSON.stringify(command === 'screen' ? filterScreen(result, { query: argument }) : result, null, 2));
  } catch (error) {
    console.error(JSON.stringify({ code: error.code, message: error.message }, null, 2));
    process.exitCode = 1;
  }
}
