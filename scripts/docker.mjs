/**
 * npm run docker — one-command Docker start (B-05). Finds the Part 3 suite next to this folder
 * (as "SuperCom" or "offenderwatch-qa-suite", or SUITE_DIR) and runs `docker compose up --build`.
 */
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');

/**
 * On Windows, npm/npx/docker are .cmd shims that need a shell. Node (DEP0190) warns when an args array is
 * combined with `shell: true`, because the args are not escaped — so build ONE quoted command line instead.
 */
function shellCommand(command, args) {
  const quote = (a) => (/[\s"&|<>^]/.test(a) ? `"${a.replace(/"/g, '\\"')}"` : a);
  return [command, ...args.map(quote)].join(' ');
}
const candidates = [process.env.SUITE_DIR, path.resolve(ROOT, '..', 'SuperCom'), path.resolve(ROOT, '..', 'offenderwatch-qa-suite')]
  .filter(Boolean).map((p) => path.resolve(p));
const suite = candidates.find((p) => fs.existsSync(path.join(p, 'playwright.config.ts')));
if (!suite) {
  console.error(`✖ Part 3 suite not found (looked in: ${candidates.join(', ')}).\n  Clone it next to this folder or set SUITE_DIR.`);
  process.exit(1);
}
console.log(`▶ Suite: ${suite}\n▶ docker compose up --build   → http://localhost:5080`);
const result = spawnSync('docker', ['compose', 'up', '--build', ...process.argv.slice(2)], {
  cwd: ROOT, stdio: 'inherit', env: { ...process.env, SUITE_DIR: suite }, // docker.exe: no shell needed
});
process.exit(result.status ?? 1);
