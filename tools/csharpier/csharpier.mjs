import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { createRequire } from 'node:module';

const supportedExtensions = new Set([
  '.axaml',
  '.config',
  '.cs',
  '.csproj',
  '.csx',
  '.props',
  '.slnx',
  '.targets',
  '.xaml',
  '.xml',
]);

const arguments_ = process.argv.slice(2);
let repoRoot = process.cwd();
let moduleRoot;
let includeGenerated = false;
const positional = [];

for (let index = 0; index < arguments_.length; index++) {
  const argument = arguments_[index];
  if (argument === '--repo-root') {
    repoRoot = path.resolve(arguments_[++index]);
  } else if (argument === '--module-root') {
    moduleRoot = path.resolve(arguments_[++index]);
  } else if (argument === '--include-generated') {
    includeGenerated = true;
  } else {
    positional.push(argument);
  }
}

if (!moduleRoot) {
  console.error('Missing --module-root.');
  process.exit(2);
}

const mode = positional.shift() ?? 'check';
if (mode !== 'check' && mode !== 'format') {
  console.error(`Unsupported command: ${mode}`);
  process.exit(2);
}

const requestedPaths = positional.length > 0 ? positional : ['.'];
const require = createRequire(path.join(moduleRoot, 'package.json'));
const { getPath } = require('@dprint/csharpier');
const { createFromBuffer } = require('@dprint/formatter');
const formatter = createFromBuffer(fs.readFileSync(getPath()));
const ignoreRules = loadIgnoreRules();
const files = discoverFiles();

let failures = 0;
for (const file of files) {
  const absolutePath = path.join(repoRoot, file);
  const source = fs.readFileSync(absolutePath, 'utf8');
  if (!includeGenerated && isGenerated(source)) {
    continue;
  }

  let formatted;
  try {
    formatted = formatter.formatText({ filePath: file, fileText: source });
  } catch (error) {
    failures++;
    console.error(`${file} - ${error.message}`);
    continue;
  }

  if (source.startsWith('\uFEFF') && !formatted.startsWith('\uFEFF')) {
    formatted = '\uFEFF' + formatted;
  }

  if (formatted === source) {
    continue;
  }

  if (mode === 'format') {
    fs.writeFileSync(absolutePath, formatted);
    console.log(`${file} - formatted`);
  } else {
    failures++;
    console.log(`${file} - not formatted`);
  }
}

if (failures > 0) {
  console.error(`${failures} file(s) not formatted`);
  process.exit(1);
}

function discoverFiles() {
  const allFiles = listRepoFiles();
  const selected = new Set();
  for (const requestedPath of requestedPaths) {
    const absolutePath = path.resolve(repoRoot, requestedPath);
    const relativePath = path.relative(repoRoot, absolutePath);
    if (relativePath.startsWith('..')) {
      continue;
    }

    if (fs.existsSync(absolutePath) && fs.statSync(absolutePath).isFile()) {
      addIfSupported(relativePath);
      continue;
    }

    const prefix = relativePath === '' ? '' : `${toPosix(relativePath)}/`;
    for (const file of allFiles) {
      if (file.startsWith(prefix)) {
        addIfSupported(file);
      }
    }
  }

  return [...selected].sort();

  function addIfSupported(file) {
    const normalized = toPosix(file);
    if (!supportedExtensions.has(path.extname(normalized).toLowerCase())) {
      return;
    }

    if (isIgnored(normalized)) {
      return;
    }

    selected.add(normalized);
  }
}

function listRepoFiles() {
  try {
    const output = execFileSync(
      'git',
      ['ls-files', '--cached', '--others', '--exclude-standard', '-z'],
      { cwd: repoRoot, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] },
    );
    return output.split('\0').filter(Boolean).map(toPosix);
  } catch {
    return walk('.');
  }
}

function walk(directory) {
  const results = [];
  for (const entry of fs.readdirSync(path.join(repoRoot, directory), { withFileTypes: true })) {
    if (entry.name === '.git' || entry.name === 'bin' || entry.name === 'obj' || entry.name === 'node_modules') {
      continue;
    }

    const relativePath = directory === '.' ? entry.name : `${directory}/${entry.name}`;
    if (entry.isDirectory()) {
      results.push(...walk(relativePath));
    } else {
      results.push(relativePath);
    }
  }

  return results;
}

function loadIgnoreRules() {
  const ignoreFile = path.join(repoRoot, '.csharpierignore');
  if (!fs.existsSync(ignoreFile)) {
    return [];
  }

  return fs
    .readFileSync(ignoreFile, 'utf8')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line !== '' && !line.startsWith('#'))
    .map((line) => {
      const negated = line.startsWith('!');
      const pattern = negated ? line.slice(1) : line;
      return { negated, matcher: globToRegExp(pattern) };
    });
}

function isIgnored(file) {
  let ignored = false;
  for (const rule of ignoreRules) {
    if (rule.matcher.test(file)) {
      ignored = !rule.negated;
    }
  }

  return ignored;
}

function globToRegExp(pattern) {
  let normalized = toPosix(pattern);
  const anchored = normalized.startsWith('/');
  if (anchored) {
    normalized = normalized.slice(1);
  }
  if (normalized.endsWith('/')) {
    normalized += '**';
  }

  const hasSlash = normalized.includes('/');
  let expression = '';
  for (let index = 0; index < normalized.length; index++) {
    const character = normalized[index];
    if (character === '*') {
      if (normalized[index + 1] === '*') {
        while (normalized[index + 1] === '*') {
          index++;
        }
        if (normalized[index + 1] === '/') {
          index++;
          expression += '(?:.*/)?';
        } else {
          expression += '.*';
        }
      } else {
        expression += '[^/]*';
      }
    } else if (character === '?') {
      expression += '[^/]';
    } else if ('\\^$.|+()[]{}'.includes(character)) {
      expression += `\\${character}`;
    } else {
      expression += character;
    }
  }

  const prefix = anchored || hasSlash ? '^' : '(?:^|.*/)';
  return new RegExp(`${prefix}${expression}$`);
}

function isGenerated(source) {
  return /<auto-generated\b|<autogenerated\b/i.test(source.slice(0, 2000));
}

function toPosix(value) {
  return value.split(path.sep).join('/');
}
