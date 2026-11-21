/**
 * One-command startup (B-05):  npm start
 *
 *   1. checks the prerequisites (.NET 8+ SDK, Node 20+, the Part 3 suite folder),
 *   2. installs the client's dependencies if needed and builds it into the API's wwwroot,
 *   3. starts the API, which then serves EVERYTHING on one port: UI, REST API, SignalR, Swagger.
 *      The SQLite database is created from the EF Core migrations on first start.
 *
 * Open http://localhost:5080 and sign in (operator / operator123 or viewer / viewer123).
 */
import { spawnSync, spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const CLIENT = path.join(ROOT, 'client');
const API = path.join(ROOT, 'server', 'src', 'TestPlatform.Api');
const SUITE = path.resolve(ROOT, '..', 'SuperCom');
const isWindows = process.platform === 'win32';

function fail(message) {
  console.error(`\n✖ ${message}\n`);
  process.exit(1);
}

function run(command, args, cwd, label) {
  console.log(`\n▶ ${label}`);
  const result = spawnSync(command, args, { cwd, stdio: 'inherit', shell: isWindows });
  if (result.status !== 0) fail(`${label} failed (exit code ${result.status}).`);
}

function check(command, args, what) {
  const result = spawnSync(command, args, { encoding: 'utf8', shell: isWindows });
  if (result.status !== 0) fail(`${what} is required but was not found. Install it and try again.`);
  return result.stdout.trim();
}

// 1 — prerequisites
const sdks = check('dotnet', ['--list-sdks'], '.NET SDK 8 or later');
if (!/^(8|9|1\d)\./m.test(sdks)) fail('.NET SDK 8 or later is required.');
const nodeMajor = Number(process.versions.node.split('.')[0]);
if (nodeMajor < 20) fail(`Node 20+ is required (found ${process.versions.node}).`);
if (!fs.existsSync(path.join(SUITE, 'playwright.config.ts'))) {
  console.warn(`⚠ Part 3 suite not found at ${SUITE}. The platform starts, but runs cannot be launched until Suite:WorkingDirectory points to it.`);
} else if (!fs.existsSync(path.join(SUITE, 'node_modules'))) {
  run('npm', ['install', '--no-audit', '--no-fund'], SUITE, 'Installing the Part 3 suite dependencies');
  run('npx', ['playwright', 'install', 'chromium'], SUITE, 'Installing the Chromium browser for the suite');
}

// 2 — client
if (!fs.existsSync(path.join(CLIENT, 'node_modules'))) run('npm', ['install', '--no-audit', '--no-fund'], CLIENT, 'Installing client dependencies');
run('npm', ['run', 'build'], CLIENT, 'Building the React client into the API (wwwroot)');

// 3 — API (serves the built client too)
console.log('\n▶ Starting the platform on http://localhost:5080  (Ctrl+C to stop)\n');
const api = spawn('dotnet', ['run', '--no-launch-profile', '--urls', 'http://localhost:5080'], {
  cwd: API, stdio: 'inherit', shell: isWindows, env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Production' },
});
api.on('exit', (code) => process.exit(code ?? 0));
for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => api.kill(signal));
