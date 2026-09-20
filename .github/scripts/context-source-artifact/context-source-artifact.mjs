import crypto from 'node:crypto'
import fs from 'node:fs/promises'
import {constants} from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import {fileURLToPath} from 'node:url'
import {DefaultArtifactClient} from '@actions/artifact'

const allowed = new Set(['manifest.json', 'bundle.sha256', 'club-elo/source.html'])
const limits = new Map([['manifest.json', 524288n], ['bundle.sha256', 65n], ['club-elo/source.html', 2097152n]])
const decimal = /^[1-9][0-9]{0,18}$/
const token = /^[0-9a-f]{32}$/
const maxInt64 = 9223372036854775807n
const fixedScratch = 'kicktippai-github-artifact'
const competition = 'bundesliga-2026-27'
const productionScope = 'production-live'

function fail(code) { throw new Error(code) }
function exactPositiveInteger(value) {
  if (!decimal.test(value ?? '')) fail('GITHUB_ARTIFACT_RUNTIME_INVALID')
  const parsed = BigInt(value)
  if (parsed > maxInt64) fail('GITHUB_ARTIFACT_RUNTIME_INVALID')
  return parsed
}
function appendLp32(hash, value) {
  const bytes = Buffer.from(value, 'utf8')
  if (bytes.length > 0xffffffff) fail('GITHUB_ARTIFACT_RUNTIME_INVALID')
  const length = Buffer.alloc(4); length.writeUInt32BE(bytes.length)
  hash.update(length); hash.update(bytes)
}
export function cycleStorageId(repositoryId, runId) {
  const hash = crypto.createHash('sha256')
  for (const value of ['context-cycle-storage/v1', competition, productionScope, `gha:${repositoryId}:${runId}`]) appendLp32(hash, value)
  return hash.digest('hex')
}
export function parseArguments(values) {
  if (values.length !== 9 || values[0] !== 'upload') fail('GITHUB_ARTIFACT_ARGUMENTS_INVALID')
  const result = {operation: values[0]}
  for (let index = 1; index < values.length; index += 2) {
    const key = values[index]; const value = values[index + 1]
    if (value === undefined) fail('GITHUB_ARTIFACT_ARGUMENTS_INVALID')
    if (key === '--name') result.name = value
    else if (key === '--content') result.content = value
    else if (key === '--compression-level') result.compressionLevel = Number(value)
    else if (key === '--retention-days') result.retentionDays = Number(value)
    else fail('GITHUB_ARTIFACT_ARGUMENTS_INVALID')
  }
  if (!path.isAbsolute(result.content) || result.compressionLevel !== 0 || result.retentionDays !== 7) fail('GITHUB_ARTIFACT_ARGUMENTS_INVALID')
  return result
}
export function validateRuntime(environment = process.env) {
  if (environment.GITHUB_REPOSITORY !== 'ehonda/KicktippAi' || !environment.ACTIONS_RUNTIME_TOKEN || !environment.ACTIONS_RESULTS_URL || !path.isAbsolute(environment.GITHUB_WORKSPACE ?? '')) fail('GITHUB_ARTIFACT_RUNTIME_INVALID')
  const repositoryId = exactPositiveInteger(environment.GITHUB_REPOSITORY_ID)
  const runId = exactPositiveInteger(environment.GITHUB_RUN_ID)
  const cycleId = `gha:${repositoryId}:${runId}`
  const storageId = cycleStorageId(repositoryId.toString(), runId.toString())
  return {repositoryId: repositoryId.toString(), runId: runId.toString(), cycleId, storageId, artifactName: `bundesliga-context-source-bundle-${storageId}`}
}
async function canonicalDirectory(directory) {
  const details = await fs.lstat(directory)
  if (!details.isDirectory() || details.isSymbolicLink()) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
  return fs.realpath(directory)
}
function samePath(left, right) { const resolvedLeft = path.resolve(left); const resolvedRight = path.resolve(right); return process.platform === 'win32' ? resolvedLeft.toLocaleLowerCase('en-US') === resolvedRight.toLocaleLowerCase('en-US') : resolvedLeft === resolvedRight }
function contained(root, candidate) { const relative = path.relative(root, candidate); return relative !== '' && !relative.startsWith(`..${path.sep}`) && relative !== '..' && !path.isAbsolute(relative) }
async function requireCanonicalAncestors(tempRoot, contentRoot) {
  let current = tempRoot
  for (const segment of [fixedScratch, path.basename(path.dirname(contentRoot)), 'content']) {
    current = path.join(current, segment)
    await canonicalDirectory(current)
  }
}
export async function validateScratchRoot(root) {
  if (!path.isAbsolute(root)) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
  const raw = path.resolve(root)
  const temp = await canonicalDirectory(os.tmpdir())
  const fixed = path.join(temp, fixedScratch)
  const cycle = path.basename(path.dirname(raw))
  const expected = path.join(fixed, cycle, 'content')
  if (!token.test(cycle) || !samePath(raw, expected)) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
  await requireCanonicalAncestors(temp, raw)
  const resolved = await canonicalDirectory(raw)
  if (!samePath(resolved, raw) || !contained(temp, resolved)) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
  return resolved
}
async function validateWorkspace(environment) {
  const raw = path.resolve(environment.GITHUB_WORKSPACE)
  const canonical = await canonicalDirectory(raw)
  if (!samePath(raw, canonical)) fail('GITHUB_ARTIFACT_WORKSPACE_INVALID')
  return canonical
}
export async function filesBelow(root) {
  try { return (await admitTree(root)).files } catch { fail('GITHUB_ARTIFACT_CONTENT_INVALID') }
}
function sameSnapshot(left, right) {
  return ['dev', 'ino', 'mode', 'nlink', 'size', 'mtimeNs', 'ctimeNs'].every(key => left[key] === right[key])
}
async function finiteEntries(directory, maximum) {
  const handle = await fs.opendir(directory); const names = []
  try {
    let entry
    while ((entry = await handle.read()) !== null) {
      if (names.length === maximum) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
      names.push(entry.name)
    }
  } finally { await handle.close() }
  return names.sort()
}
async function admitTree(root) {
  const absoluteRoot = await validateScratchRoot(root)
  const snapshots = new Map(); const files = []; let total = 0n
  async function directory(relative, maximum) {
    const full = path.join(absoluteRoot, relative)
    const details = await fs.lstat(full, {bigint: true})
    if (!details.isDirectory() || details.isSymbolicLink() || !samePath(await fs.realpath(full), full)) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
    snapshots.set(relative, details)
    return finiteEntries(full, maximum)
  }
  const entries = await directory('', 3)
  if (!entries.includes('manifest.json') || !entries.includes('bundle.sha256') || entries.some(name => !['manifest.json', 'bundle.sha256', 'club-elo'].includes(name))) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
  const names = ['manifest.json', 'bundle.sha256']
  if (entries.includes('club-elo')) {
    const children = await directory('club-elo', 1)
    if (children.length !== 1 || children[0] !== 'source.html') fail('GITHUB_ARTIFACT_CONTENT_INVALID')
    names.push('club-elo/source.html')
  }
  for (const relative of names) {
    const full = path.join(absoluteRoot, relative)
    const details = await fs.lstat(full, {bigint: true})
    if (!allowed.has(relative) || !details.isFile() || details.isSymbolicLink() || details.nlink !== 1n || details.size > limits.get(relative)) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
    const real = await fs.realpath(full)
    if (!samePath(real, full) || !contained(absoluteRoot, real)) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
    total += details.size
    if (total > 3145728n) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
    snapshots.set(relative, details); files.push(real)
  }
  return {root: absoluteRoot, files: files.sort((left, right) => left.localeCompare(right, 'en')), snapshots}
}
async function validateManifest(tree, runtime) {
  const expected = tree.snapshots.get('manifest.json')
  const handle = await fs.open(path.join(tree.root, 'manifest.json'), constants.O_RDONLY | (constants.O_NOFOLLOW ?? 0))
  let bytes; let length = 0
  try {
    if (!sameSnapshot(expected, await handle.stat({bigint: true}))) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
    bytes = Buffer.alloc(524289)
    while (length < bytes.length) {
      const {bytesRead} = await handle.read(bytes, length, bytes.length - length, length)
      if (bytesRead === 0) break
      length += bytesRead
    }
    if (length > 524288 || BigInt(length) !== expected.size || !sameSnapshot(expected, await handle.stat({bigint: true}))) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
  } finally { await handle.close() }
  let value
  try { value = JSON.parse(bytes.subarray(0, length).toString('utf8')) } catch { fail('GITHUB_ARTIFACT_CONTENT_INVALID') }
  if (!value || typeof value !== 'object' || Array.isArray(value) || value.competition !== competition || value.scope !== productionScope || value.cycleId !== runtime.cycleId || value.cycleStorageId !== runtime.storageId) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
}
export async function upload(args, client = new DefaultArtifactClient(), environment = process.env) {
  const runtime = validateRuntime(environment)
  if (args.name !== runtime.artifactName) fail('GITHUB_ARTIFACT_RUNTIME_INVALID')
  await validateWorkspace(environment)
  let tree
  try {
    tree = await admitTree(args.content)
    await validateManifest(tree, runtime)
    // Re-admit the exact finite tree immediately before handing paths to the
    // pinned producer. Changes in file identity, kind, links, size or time fail.
    const current = await admitTree(tree.root)
    if (tree.snapshots.size !== current.snapshots.size || [...tree.snapshots].some(([name, snapshot]) => !current.snapshots.has(name) || !sameSnapshot(snapshot, current.snapshots.get(name)))) fail('GITHUB_ARTIFACT_CONTENT_INVALID')
  } catch { fail('GITHUB_ARTIFACT_CONTENT_INVALID') }
  await client.uploadArtifact(runtime.artifactName, tree.files, tree.root, {compressionLevel: 0, retentionDays: 7})
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { await upload(parseArguments(process.argv.slice(2))) } catch { process.exitCode = 1 }
}
