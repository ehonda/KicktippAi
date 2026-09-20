import assert from 'node:assert/strict'
import crypto from 'node:crypto'
import {fileURLToPath, pathToFileURL} from 'node:url'
import fs from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import nodeTest from 'node:test'
const test = process.argv.includes('--w2r-producer-fixture') ? () => {} : nodeTest
import {cycleStorageId, filesBelow, parseArguments, upload, validateRuntime} from './context-source-artifact.mjs'

const workspace = path.resolve(process.cwd(), '../../..')
const manifest = JSON.stringify({competition: 'bundesliga-2026-27', scope: 'production-live', cycleId: 'gha:123:456', cycleStorageId: cycleStorageId('123', '456')})
const runtime = {GITHUB_REPOSITORY: 'ehonda/KicktippAi', GITHUB_REPOSITORY_ID: '123', GITHUB_RUN_ID: '456', ACTIONS_RUNTIME_TOKEN: 'x', ACTIONS_RESULTS_URL: 'https://results.example', GITHUB_WORKSPACE: workspace}
async function scratch(token = crypto.randomUUID().replaceAll('-', '')) {
  const root = path.join(os.tmpdir(), 'kicktippai-github-artifact', token, 'content')
  await fs.mkdir(root, {recursive: true}); return root
}
async function clean(root) {
  const fixed = path.resolve(os.tmpdir(), 'kicktippai-github-artifact')
  const parent = path.dirname(path.resolve(root))
  assert.equal(path.dirname(parent), fixed)
  assert.match(path.basename(parent), /^[0-9a-f]{32}$/)
  assert.equal(path.basename(path.resolve(root)), 'content')
  await fs.rm(parent, {recursive: true, force: true})
}
function args(root, name = `bundesliga-context-source-bundle-${cycleStorageId('123', '456')}`) { return parseArguments(['upload', '--name', name, '--content', root, '--compression-level', '0', '--retention-days', '7']) }

test('derives the exact cycle storage artifact name with BigInt-safe GitHub ids', () => {
  const valid = validateRuntime(runtime)
  assert.equal(valid.cycleId, 'gha:123:456'); assert.equal(valid.storageId, cycleStorageId('123', '456')); assert.equal(valid.artifactName, `bundesliga-context-source-bundle-${valid.storageId}`)
  assert.throws(() => validateRuntime({...runtime, GITHUB_RUN_ID: '9223372036854775808'})); assert.throws(() => validateRuntime({...runtime, GITHUB_REPOSITORY_ID: '1e3'}))
})
test('accepts only immutable absolute upload arguments', async () => {
  const root = await scratch('b'.repeat(32))
  try { assert.equal(args(root).retentionDays, 7); assert.throws(() => parseArguments(['upload', '--name', 'x', '--content', 'relative', '--compression-level', '0', '--retention-days', '7'])) } finally { await clean(root) }
})
test('requires exact runtime identity and artifact binding without exposing values', async () => {
  const root = await scratch('c'.repeat(32))
  try { await fs.writeFile(path.join(root, 'manifest.json'), manifest); await fs.writeFile(path.join(root, 'bundle.sha256'), 'x'); await assert.rejects(() => upload(args(root, `bundesliga-context-source-bundle-${'a'.repeat(64)}`), {uploadArtifact: async () => {}}, runtime)); await assert.rejects(() => upload(args(root), {uploadArtifact: async () => {}}, {...runtime, GITHUB_REPOSITORY: 'other/repo'})) } finally { await clean(root) }
})
test('rejects a manifest bound to a different GitHub cycle before upload', async () => {
  const root = await scratch('1'.repeat(32))
  try {
    await fs.writeFile(path.join(root, 'manifest.json'), JSON.stringify({...JSON.parse(manifest), cycleId: 'gha:123:999'})); await fs.writeFile(path.join(root, 'bundle.sha256'), 'x')
    await assert.rejects(() => upload(args(root), {uploadArtifact: async () => {}}, runtime))
  } finally { await clean(root) }
})

test('allows only exact regular files within canonical scratch containment', async () => {
  const root = await scratch('d'.repeat(32))
  try {
    await fs.writeFile(path.join(root, 'manifest.json'), manifest); await fs.writeFile(path.join(root, 'bundle.sha256'), 'x'); await fs.mkdir(path.join(root, 'club-elo')); await fs.writeFile(path.join(root, 'club-elo', 'source.html'), '<html>')
    assert.equal((await filesBelow(root)).length, 3); await fs.writeFile(path.join(root, 'unexpected.txt'), 'x'); await assert.rejects(() => filesBelow(root)); await fs.rm(path.join(root, 'unexpected.txt'))
    await fs.link(path.join(root, 'manifest.json'), path.join(root, 'linked.json')); await assert.rejects(() => filesBelow(root))
  } finally { await clean(root) }
})
test('rejects scratch escapes and workspace indirection before upload', async () => {
  const root = await scratch('e'.repeat(32))
  try { await fs.writeFile(path.join(root, 'manifest.json'), manifest); await fs.writeFile(path.join(root, 'bundle.sha256'), 'x'); await assert.rejects(() => filesBelow(path.join(os.tmpdir(), 'other', 'e'.repeat(32), 'content'))); await assert.rejects(() => upload(args(root), {uploadArtifact: async () => {}}, {...runtime, GITHUB_WORKSPACE: path.join(workspace, 'does-not-exist')})) } finally { await clean(root) }
})
test('upload passes only exact files and immutable options', async () => {
  const root = await scratch('f'.repeat(32)); let call
  try { await fs.writeFile(path.join(root, 'manifest.json'), manifest); await fs.writeFile(path.join(root, 'bundle.sha256'), 'x'); await upload(args(root), {uploadArtifact: async (...values) => { call = values }}, runtime); assert.equal(call[0], validateRuntime(runtime).artifactName); assert.equal(call[1].length, 2); assert.equal(call[2], path.resolve(root)); assert.deepEqual(call[3], {compressionLevel: 0, retentionDays: 7}) } finally { await clean(root) }
})

// W2R P: every attack starts with the same valid identity/file tree. Client is a
// local spy only; these tests never call the artifact service or use credentials.
async function validTree(html = false) {
  const root = await scratch()
  await fs.writeFile(path.join(root, 'manifest.json'), manifest)
  await fs.writeFile(path.join(root, 'bundle.sha256'), 'a'.repeat(64) + '\n')
  if (html) { await fs.mkdir(path.join(root, 'club-elo')); await fs.writeFile(path.join(root, 'club-elo/source.html'), '<html>fixture</html>') }
  return root
}
async function rejectedBeforeUpload(root) {
  let uploads = 0
  await assert.rejects(() => upload(args(root), {uploadArtifact: async () => { uploads++ }}, runtime), /GITHUB_ARTIFACT_CONTENT_INVALID/)
  assert.equal(uploads, 0)
}
for (const emptyDirectory of ['unexpected', 'unexpected/deep/empty', 'club-elo']) {
  test(`P rejects empty directory shape ${emptyDirectory} before upload`, async () => {
    const root = await validTree()
    try { await fs.mkdir(path.join(root, emptyDirectory), {recursive: true}); await rejectedBeforeUpload(root) }
    finally { await clean(root) }
  })
}
for (const name of ['manifest.json', 'bundle.sha256', 'club-elo/source.html']) {
  test(`P rejects allowed-name hardlink ${name} before upload`, async () => {
    const root = await validTree(name.startsWith('club-elo/'))
    // Same-volume external file is inside test-owned parent, outside content root.
    const outside = path.join(path.dirname(root), 'outside-file')
    const target = path.join(root, name)
    try {
      await fs.rename(target, outside); await fs.link(outside, target)
      assert.equal((await fs.lstat(target)).nlink, 2)
      assert.equal(await fs.realpath(target), target) // demonstrates why realpath alone is insufficient
      await rejectedBeforeUpload(root)
    } finally { await clean(root) }
  })
}
for (const [name, limit] of [['manifest.json', 512 * 1024], ['bundle.sha256', 65], ['club-elo/source.html', 2 * 1024 * 1024]]) {
  test(`P accepts exact byte limit for ${name}`, async () => {
    const root = await validTree(name.startsWith('club-elo/')); let uploads = 0
    try {
      const value = name === 'manifest.json' ? manifest.padEnd(limit, ' ') : 'x'.repeat(limit)
      await fs.writeFile(path.join(root, name), value)
      await upload(args(root), {uploadArtifact: async () => { uploads++ }}, runtime)
      assert.equal(uploads, 1)
    } finally { await clean(root) }
  })
  test(`P rejects limit plus one for ${name} before upload`, async () => {
    const root = await validTree(name.startsWith('club-elo/'))
    try {
      const value = name === 'manifest.json' ? manifest.padEnd(limit + 1, ' ') : 'x'.repeat(limit + 1)
      await fs.writeFile(path.join(root, name), value); await rejectedBeforeUpload(root)
    } finally { await clean(root) }
  })
}
test('P rejects oversized manifest before any full-file read allocation', async t => {
  const root = await validTree(); const original = fs.readFile
  try {
    await fs.writeFile(path.join(root, 'manifest.json'), manifest.padEnd(512 * 1024 + 1, ' '))
    let fullReads = 0
    t.mock.method(fs, 'readFile', async function(file, ...options) {
      if (String(file) === path.join(root, 'manifest.json')) { fullReads++; throw new Error('FULL_MANIFEST_ALLOCATION_REACHED') }
      return original.call(this, file, ...options)
    })
    await rejectedBeforeUpload(root)
    assert.equal(fullReads, 0)
  } finally { t.mock.restoreAll(); await clean(root) }
})
test('P revalidates admitted snapshot when hash file changes after admission stat', async t => {
  const root = await validTree(); const original = fs.lstat
  try {
    let changed = false
    t.mock.method(fs, 'lstat', async function(file, ...options) {
      const snapshot = await original.call(this, file, ...options)
      if (!changed && String(file) === path.join(root, 'bundle.sha256')) {
        changed = true; await fs.writeFile(path.join(root, 'bundle.sha256'), 'x'.repeat(66))
      }
      return snapshot
    })
    await rejectedBeforeUpload(root)
    assert.equal(changed, true)
  } finally { t.mock.restoreAll(); await clean(root) }
})

test('P bounded manifest handle rejects growth during consumption and closes', async t => {
  const root = await validTree(); const originalOpen = fs.open
  let opened = 0; let closed = 0; let consumed = 0; let largestRequest = 0
  try {
    t.mock.method(fs, 'open', async function(file, ...options) {
      const handle = await originalOpen.call(this, file, ...options)
      if (String(file) !== path.join(root, 'manifest.json')) return handle
      opened++
      const read = handle.read.bind(handle); const close = handle.close.bind(handle)
      t.mock.method(handle, 'read', async (buffer, offset, length, position) => {
        largestRequest = Math.max(largestRequest, length)
        const result = await read(buffer, offset, length, position); consumed += result.bytesRead
        if (position === 0) await fs.writeFile(file, manifest.padEnd(524289, ' '))
        return result
      })
      t.mock.method(handle, 'close', async () => { closed++; return close() })
      return handle
    })
    await rejectedBeforeUpload(root)
    assert.equal(opened, 1); assert.equal(closed, 1)
    assert.ok(consumed <= 524289 && largestRequest <= 524289)
  } finally { t.mock.restoreAll(); await clean(root) }
})
for (const name of ['manifest.json', 'bundle.sha256', 'club-elo/source.html']) {
  test(`P rejects same-size identity replacement of ${name} near consumption`, async t => {
    const root = await validTree(name.startsWith('club-elo/')); const originalOpen = fs.open
    let replaced = false
    try {
      t.mock.method(fs, 'open', async function(file, ...options) {
        if (!replaced && String(file) === path.join(root, 'manifest.json')) {
          replaced = true
          const target = path.join(root, name); const original = await fs.readFile(target)
          await fs.rename(target, path.join(path.dirname(root), 'old-file'))
          await fs.writeFile(target, original)
        }
        return originalOpen.call(this, file, ...options)
      })
      await rejectedBeforeUpload(root); assert.equal(replaced, true)
    } finally { t.mock.restoreAll(); await clean(root) }
  })
}

// W2R Z exact-package provenance captured 2026-09-16 by reading the public
// package-lock tarball in memory and verifying SHA512 before inspecting its entries.
// No installed package, Node execution, generated ZIP or live service credit is
// claimed by this source proof. Fixture generation is explicitly root-gated.
const producerProof = Object.freeze({
  package: '@actions/artifact', version: '6.2.1',
  tarball: 'https://registry.npmjs.org/@actions/artifact/-/artifact-6.2.1.tgz',
  integrity: 'sha512-sJGH0mhEbEjBCw7o6SaLhUU66u27aFW8HTfkIb5Tk2/Wy0caUDc+oYQEgnuFN7a0HCpAbQyK0U6U7XUJDgDWrw==',
  lockSha256: 'df8171328c104736abe9653acf08056a0af61883d192db4fef467d4955b67ccd',
  lockHashNormalization: 'UTF-8 text with CRLF converted to LF; raw file hash also reported',
  sourceSha256: {
    'package.json': 'e21bb31fa8424754cd03c72278d78a76e50429895a9cb2babf69b4a7ba8f533a',
    'lib/internal/upload/zip.js': '8a8708fd49b2d6474a67fea2aa5e0ff56bae90401f5b6c95db0a600eb91e5a23',
    'lib/internal/upload/upload-zip-specification.js': '770ddc02798dd42627653f48aada2168d3d3af4f5c0f9f7e4b655f0ba914fe94'
  },
  transitive: {archiver: '7.0.1', 'zip-stream': '6.0.1', 'compress-commons': '6.0.2'},
  transitiveWriterSha256: {
    'archiver/lib/core.js': 'a8b28e116fef412d7503f7cc4a64b01d3d2f747a493b3d83dd97bd732ffc8b92',
    'zip-stream/index.js': '29ea55ff9cf0007853d4ac51936278547299a601a00980bb373c002f21a85186',
    'compress-commons/lib/archivers/zip/zip-archive-output-stream.js': '262cce586a8d182efb086e5be6b617958366851b53c1c95298377eec085274c4'
  },
  actualPipeline: 'getUploadZipSpecification(files, root) -> createZipUploadStream(specification, 0)',
  // Tarball zip.js invokes zip.file(sourcePath, {name: file.destinationPath});
  // no handmade Archiver imitation, uploadArtifact call or emitted-header rewrite.
  expectedProfileRequiresReview: 'classic signed descriptors; STORE; local zero CRC/sizes; central and descriptor bindings'
})
// Immutable Linux captures keep their ORIGINAL generator identity; new runs do not relabel them.
const reviewedCapture = Object.freeze({
  "diagnosisSha256": "1105295401b422d13659c971f7550d2f2aeb3b365662549d13e7c97dda07811f",
  "reviewSha256": "88b1485818713b8c175a1fc4b198b8b111dea62b289d7c6356ce2de08859fcca",
  "originalGeneratorSha256": "e18dc8370e751a30d84e1307b531c2ab38cb5cbce0a61ba44c1a1d37c469ad52",
  "image": "node:24-bookworm-slim",
  "imageDigest": "sha256:2fe369e969550cde8e867afc3fe370b260140cab4a23d467074295b42163d553",
  "npm": "11.19.0",
  "command": "node context-source-artifact.test.mjs --w2r-producer-fixture",
  "rawCaptures": {
    "producer-fixtures-run2.txt": {
      "sha256": "a511ac371ca225de777158d621c6104d23eedf8f93c527afacd86fbeb442eed8",
      "length": 9514
    },
    "producer-fixtures-run1.txt": {
      "sha256": "a511ac371ca225de777158d621c6104d23eedf8f93c527afacd86fbeb442eed8",
      "length": 9514
    }
  },
  "producer": {
    "package": "@actions/artifact",
    "version": "6.2.1",
    "tarball": "https://registry.npmjs.org/@actions/artifact/-/artifact-6.2.1.tgz",
    "integrity": "sha512-sJGH0mhEbEjBCw7o6SaLhUU66u27aFW8HTfkIb5Tk2/Wy0caUDc+oYQEgnuFN7a0HCpAbQyK0U6U7XUJDgDWrw==",
    "lockSha256": "df8171328c104736abe9653acf08056a0af61883d192db4fef467d4955b67ccd",
    "lockHashNormalization": "UTF-8 text with CRLF converted to LF; raw file hash also reported",
    "sourceSha256": {
      "package.json": "e21bb31fa8424754cd03c72278d78a76e50429895a9cb2babf69b4a7ba8f533a",
      "lib/internal/upload/zip.js": "8a8708fd49b2d6474a67fea2aa5e0ff56bae90401f5b6c95db0a600eb91e5a23",
      "lib/internal/upload/upload-zip-specification.js": "770ddc02798dd42627653f48aada2168d3d3af4f5c0f9f7e4b655f0ba914fe94"
    },
    "transitive": {
      "archiver": "7.0.1",
      "zip-stream": "6.0.1",
      "compress-commons": "6.0.2"
    },
    "transitiveWriterSha256": {
      "archiver/lib/core.js": "a8b28e116fef412d7503f7cc4a64b01d3d2f747a493b3d83dd97bd732ffc8b92",
      "zip-stream/index.js": "29ea55ff9cf0007853d4ac51936278547299a601a00980bb373c002f21a85186",
      "compress-commons/lib/archivers/zip/zip-archive-output-stream.js": "262cce586a8d182efb086e5be6b617958366851b53c1c95298377eec085274c4"
    },
    "actualPipeline": "getUploadZipSpecification(files, root) -> createZipUploadStream(specification, 0)",
    "expectedProfileRequiresReview": "classic signed descriptors; STORE; local zero CRC/sizes; central and descriptor bindings",
    "node": "v24.21.0",
    "platform": "linux",
    "arch": "x64",
    "transitivePackageSha256": {
      "archiver": "71f9d2abd62fc121c3f5c7ccb75a148cf1e8ce0b58ae6b86ea150cf719b86133",
      "zip-stream": "83ad75d717b4403a74b909e05363f6f7696eefdec04b49e783bf4546e84dab7f",
      "compress-commons": "d405726826d0c72939d487c8b81b1a278741e825f7a7e2ea7748c9bd9cb5c57c"
    },
    "rawLockSha256": "df8171328c104736abe9653acf08056a0af61883d192db4fef467d4955b67ccd",
    "inputTimestamp": "2024-01-01T00:00:00.000Z",
    "inputMode": "0644",
    "compressionLevel": 0
  }
})
const reviewedRawFixtures = Object.freeze([
  {
    "length": 493,
    "sha256": "2fcaf91b679e82239bb94c15ef35a90690c88b566010e6aef9fec94f144282b5",
    "base64": "UEsDBBQACAAAAAAAIVgAAAAAAAAAAAAAAAANAAAAYnVuZGxlLnNoYTI1NmFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWEKUEsHCI0XXylBAAAAQQAAAFBLAwQUAAgAAAAAACFYAAAAAAAAAAAAAAAADQAAAG1hbmlmZXN0Lmpzb257ImNvbXBldGl0aW9uIjoiYnVuZGVzbGlnYS0yMDI2LTI3Iiwic2NvcGUiOiJwcm9kdWN0aW9uLWxpdmUiLCJjeWNsZUlkIjoiZ2hhOjEyMzo0NTYiLCJjeWNsZVN0b3JhZ2VJZCI6ImQyMWM0OGY0Y2U3YjNhYmIwMDg1OWY3MGIwZjJhMjE2OTcxYTc3ODdiZmU3YjI5ZDkyMmQ5Y2ZmY2E2YTY4YjYifVBLBwh8aEhyqgAAAKoAAABQSwECLQMUAAgAAAAAACFYjRdfKUEAAABBAAAADQAAAAAAAAAAACAApIEAAAAAYnVuZGxlLnNoYTI1NlBLAQItAxQACAAAAAAAIVh8aEhyqgAAAKoAAAANAAAAAAAAAAAAIACkgXwAAABtYW5pZmVzdC5qc29uUEsFBgAAAAACAAIAdgAAAGEBAAAAAA==",
    "input": {
      "bundle.sha256": "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYQo=",
      "manifest.json": "eyJjb21wZXRpdGlvbiI6ImJ1bmRlc2xpZ2EtMjAyNi0yNyIsInNjb3BlIjoicHJvZHVjdGlvbi1saXZlIiwiY3ljbGVJZCI6ImdoYToxMjM6NDU2IiwiY3ljbGVTdG9yYWdlSWQiOiJkMjFjNDhmNGNlN2IzYWJiMDA4NTlmNzBiMGYyYTIxNjk3MWE3Nzg3YmZlN2IyOWQ5MjJkOWNmZmNhNmE2OGI2In0="
    },
    "centralStart": 353,
    "centralSize": 118
  },
  {
    "length": 645,
    "sha256": "36f81428b5f589cd8c1fa5f3cb626d6d3316eb75efc4c751b9291204abb74c7c",
    "base64": "UEsDBBQACAAAAAAAIVgAAAAAAAAAAAAAAAANAAAAYnVuZGxlLnNoYTI1NmFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWEKUEsHCI0XXylBAAAAQQAAAFBLAwQUAAgAAAAAACFYAAAAAAAAAAAAAAAAFAAAAGNsdWItZWxvL3NvdXJjZS5odG1sPGh0bWw+Zml4dHVyZTwvaHRtbD5QSwcIu1EiAxQAAAAUAAAAUEsDBBQACAAAAAAAIVgAAAAAAAAAAAAAAAANAAAAbWFuaWZlc3QuanNvbnsiY29tcGV0aXRpb24iOiJidW5kZXNsaWdhLTIwMjYtMjciLCJzY29wZSI6InByb2R1Y3Rpb24tbGl2ZSIsImN5Y2xlSWQiOiJnaGE6MTIzOjQ1NiIsImN5Y2xlU3RvcmFnZUlkIjoiZDIxYzQ4ZjRjZTdiM2FiYjAwODU5ZjcwYjBmMmEyMTY5NzFhNzc4N2JmZTdiMjlkOTIyZDljZmZjYTZhNjhiNiJ9UEsHCHxoSHKqAAAAqgAAAFBLAQItAxQACAAAAAAAIViNF18pQQAAAEEAAAANAAAAAAAAAAAAIACkgQAAAABidW5kbGUuc2hhMjU2UEsBAi0DFAAIAAAAAAAhWLtRIgMUAAAAFAAAABQAAAAAAAAAAAAgAKSBfAAAAGNsdWItZWxvL3NvdXJjZS5odG1sUEsBAi0DFAAIAAAAAAAhWHxoSHKqAAAAqgAAAA0AAAAAAAAAAAAgAKSB0gAAAG1hbmlmZXN0Lmpzb25QSwUGAAAAAAMAAwC4AAAAtwEAAAAA",
    "input": {
      "bundle.sha256": "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYQo=",
      "club-elo/source.html": "PGh0bWw+Zml4dHVyZTwvaHRtbD4=",
      "manifest.json": "eyJjb21wZXRpdGlvbiI6ImJ1bmRlc2xpZ2EtMjAyNi0yNyIsInNjb3BlIjoicHJvZHVjdGlvbi1saXZlIiwiY3ljbGVJZCI6ImdoYToxMjM6NDU2IiwiY3ljbGVTdG9yYWdlSWQiOiJkMjFjNDhmNGNlN2IzYWJiMDA4NTlmNzBiMGYyYTIxNjk3MWE3Nzg3YmZlN2IyOWQ5MjJkOWNmZmNhNmE2OGI2In0="
    },
    "centralStart": 439,
    "centralSize": 184
  }
])
const sha256 = bytes => crypto.createHash('sha256').update(bytes).digest('hex')
async function pinnedProducer() {
  assert.equal(Number(process.versions.node.split('.')[0]), 24, 'Producer proof requires admitted Node 24')
  const entry = fileURLToPath(import.meta.resolve('@actions/artifact'))
  const packageRoot = path.resolve(path.dirname(entry), '..')
  const lockBytes = await fs.readFile(new URL('./package-lock.json', import.meta.url))
  assert.equal(sha256(lockBytes.toString('utf8').replaceAll('\r\n', '\n')), producerProof.lockSha256)
  const lock = JSON.parse(lockBytes)
  assert.equal(lock.packages['node_modules/@actions/artifact'].integrity, producerProof.integrity)
  for (const [relative, expected] of Object.entries(producerProof.sourceSha256)) {
    assert.equal(sha256(await fs.readFile(path.join(packageRoot, relative))), expected, `Pinned package source ${relative}`)
  }
  const packageJson = JSON.parse(await fs.readFile(path.join(packageRoot, 'package.json')))
  assert.equal(packageJson.version, producerProof.version)
  const transitiveSources = {}
  for (const [name, version] of Object.entries(producerProof.transitive)) {
    assert.equal(lock.packages[`node_modules/${name}`].version, version)
    const packageFile = new URL(`./node_modules/${name}/package.json`, import.meta.url)
    const bytes = await fs.readFile(packageFile)
    assert.equal(JSON.parse(bytes).version, version)
    transitiveSources[name] = sha256(bytes)
  }
  for (const [relative, expected] of Object.entries(producerProof.transitiveWriterSha256)) {
    assert.equal(sha256(await fs.readFile(new URL(`./node_modules/${relative}`, import.meta.url))), expected, `Pinned transitive writer ${relative}`)
  }
  assert.deepEqual(transitiveSources, reviewedCapture.producer.transitivePackageSha256, 'Reviewed transitive package metadata hashes')
  // Resolve from public entry, then import verified installed internal files by URL.
  const {getUploadZipSpecification} = await import(pathToFileURL(path.join(packageRoot, 'lib/internal/upload/upload-zip-specification.js')).href)
  const {createZipUploadStream} = await import(pathToFileURL(path.join(packageRoot, 'lib/internal/upload/zip.js')).href)
  return {getUploadZipSpecification, createZipUploadStream, transitiveSources, rawLockSha256: sha256(lockBytes)}
}
const reviewedEntryValues = Object.freeze({
  'bundle.sha256': {length: 65, crc: 0x295f178d, sha256: '44c2336fedab8ff6a85c74c2b94165377b0981f526adb9487895ca6314165e86', descriptorHex: '504b07088d175f294100000041000000'},
  'manifest.json': {length: 170, crc: 0x7248687c, sha256: '38ebe2bd8fe5e8d122861f17d7c76f66b1acd3f652343f9cddc1eab858989734', descriptorHex: '504b07087c684872aa000000aa000000'},
  'club-elo/source.html': {length: 20, crc: 0x032251bb, sha256: '3ec85d118c49f07362673b7837c60410d3f39e6fa73414a7c6fcd58094f87eb4', descriptorHex: '504b0708bb5122031400000014000000'}
})
function crc32(bytes) {
  let crc = 0xffffffff
  for (const value of bytes) {
    crc ^= value
    for (let bit = 0; bit < 8; bit++) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0)
  }
  return (~crc) >>> 0
}
function reviewedEntryProfile(name, payloadBase64) {
  const expected = reviewedEntryValues[name]
  assert.ok(expected, 'Exact allowed entry name')
  const nameBytes = Buffer.from(name, 'ascii'), nameLength = nameBytes.length
  return {name, creator: 0x032d, version: 20, flags: 8, method: 0, time: 0, date: 0x5821,
    crc: expected.crc, compressed: expected.length, expanded: expected.length,
    nameLength, nameHex: nameBytes.toString('hex'), extraLength: 0, commentLength: 0, diskStart: 0,
    internalAttributes: 0, attributes: 0x81a40020,
    localVersion: 20, localFlags: 8, localMethod: 0, localTime: 0, localDate: 0x5821,
    localCrc: 0, localCompressed: 0, localExpanded: 0, localNameLength: nameLength, localNameHex: nameBytes.toString('hex'), localExtraLength: 0,
    localHeaderLength: 30 + nameLength, localRecordLength: 30 + nameLength + expected.length + 16, centralRecordLength: 46 + nameLength,
    descriptorSignature: 0x08074b50, descriptorCrc: expected.crc, descriptorCompressed: expected.length, descriptorExpanded: expected.length,
    descriptorLength: 16, descriptorHex: expected.descriptorHex, payloadSha256: expected.sha256, payloadBase64}
}
function reviewedProfile(golden) {
  const names = Object.keys(golden.input).sort()
  return {archive: {length: golden.length, disk: 0, centralDisk: 0, entriesOnDisk: names.length, entryCount: names.length,
    localAreaLength: golden.centralStart, centralSize: golden.centralSize, eocdLength: 22, commentLength: 0},
    entries: names.map(name => reviewedEntryProfile(name, golden.input[name]))}
}
function validateProducerZip(bytes, input) {
  // Strict fixture validator, not a permissive ZIP parser. Every byte is covered
  // before ONLY positions/order are projected away. Payload signatures are never scanned.
  const range = (offset, length, limit, label) => {
    assert.ok(Number.isSafeInteger(offset) && Number.isSafeInteger(length) && offset >= 0 && length >= 0 && offset <= limit && length <= limit - offset, label)
  }
  const expectedNames = Object.keys(input).sort()
  const golden = reviewedRawFixtures.find(fixture => Object.keys(fixture.input).length === expectedNames.length)
  assert.ok(golden, 'Only the reviewed two/three-file shapes are admitted')
  assert.deepEqual(input, golden.input, 'Generation inputs must equal the reviewed payloads')
  assert.equal(bytes.length, golden.length, 'Exact fixture envelope length')
  const eocd = bytes.length - 22
  range(eocd, 22, bytes.length, 'Complete terminal EOCD')
  assert.equal(bytes.readUInt32LE(eocd), 0x06054b50)
  const disk = bytes.readUInt16LE(eocd + 4), centralDisk = bytes.readUInt16LE(eocd + 6)
  const entriesOnDisk = bytes.readUInt16LE(eocd + 8), count = bytes.readUInt16LE(eocd + 10)
  const centralSize = bytes.readUInt32LE(eocd + 12), centralStart = bytes.readUInt32LE(eocd + 16), commentLength = bytes.readUInt16LE(eocd + 20)
  assert.equal(disk, 0); assert.equal(centralDisk, 0); assert.equal(entriesOnDisk, count); assert.equal(count, expectedNames.length); assert.equal(commentLength, 0)
  range(centralStart, centralSize, eocd, 'Bounded classic central directory')
  assert.equal(centralStart + centralSize, eocd, 'No central/EOCD gaps, ZIP64 records or trailing bytes')
  let cursor = centralStart, expectedLocalStart = 0
  const inventory = [], seenNames = new Set(), seenLocals = new Set()
  for (let sequenceIndex = 0; sequenceIndex < count; sequenceIndex++) {
    range(cursor, 46, eocd, 'Complete central fixed header')
    assert.equal(bytes.readUInt32LE(cursor), 0x02014b50)
    const nameLength = bytes.readUInt16LE(cursor + 28), extraLength = bytes.readUInt16LE(cursor + 30), entryCommentLength = bytes.readUInt16LE(cursor + 32)
    const local = bytes.readUInt32LE(cursor + 42), compressed = bytes.readUInt32LE(cursor + 20)
    const centralRecordLength = 46 + nameLength + extraLength + entryCommentLength
    range(cursor, centralRecordLength, eocd, 'Complete central variable fields')
    assert.equal(extraLength, 0); assert.equal(entryCommentLength, 0)
    const nameBytes = bytes.subarray(cursor + 46, cursor + 46 + nameLength), name = nameBytes.toString('utf8')
    assert.ok(expectedNames.includes(name) && !seenNames.has(name), 'Exact, unique allowed names; no case/path aliases')
    assert.deepEqual(nameBytes, Buffer.from(name, 'ascii'), 'Exact ASCII name bytes, not a lossy decode')
    assert.ok(!seenLocals.has(local), 'Unique local references'); seenNames.add(name); seenLocals.add(local)
    assert.equal(local, expectedLocalStart, 'Producer central sequence equals local sequence, with no prefix/gap/overlap')
    range(local, 30, centralStart, 'Complete bounded local fixed header')
    assert.equal(bytes.readUInt32LE(local), 0x04034b50)
    const localNameLength = bytes.readUInt16LE(local + 26), localExtraLength = bytes.readUInt16LE(local + 28)
    const localHeaderLength = 30 + localNameLength + localExtraLength
    range(local, localHeaderLength, centralStart, 'Complete bounded local variable fields')
    assert.equal(localExtraLength, 0)
    const localNameBytes = bytes.subarray(local + 30, local + 30 + localNameLength)
    assert.deepEqual(localNameBytes, nameBytes, 'Local/central raw names bind exactly')
    const dataStart = local + localHeaderLength, descriptorOffset = dataStart + compressed
    range(dataStart, compressed, centralStart, 'Payload ends before central directory')
    range(descriptorOffset, 16, centralStart, 'Exactly one complete classic signed descriptor')
    const intervalEnd = descriptorOffset + 16, payload = bytes.subarray(dataStart, descriptorOffset)
    const record = {name, creator: bytes.readUInt16LE(cursor + 4), version: bytes.readUInt16LE(cursor + 6),
      flags: bytes.readUInt16LE(cursor + 8), method: bytes.readUInt16LE(cursor + 10), time: bytes.readUInt16LE(cursor + 12), date: bytes.readUInt16LE(cursor + 14),
      crc: bytes.readUInt32LE(cursor + 16), compressed, expanded: bytes.readUInt32LE(cursor + 24),
      nameLength, nameHex: nameBytes.toString('hex'), extraLength, commentLength: entryCommentLength, diskStart: bytes.readUInt16LE(cursor + 34),
      internalAttributes: bytes.readUInt16LE(cursor + 36), attributes: bytes.readUInt32LE(cursor + 38),
      localVersion: bytes.readUInt16LE(local + 4), localFlags: bytes.readUInt16LE(local + 6), localMethod: bytes.readUInt16LE(local + 8),
      localTime: bytes.readUInt16LE(local + 10), localDate: bytes.readUInt16LE(local + 12), localCrc: bytes.readUInt32LE(local + 14), localCompressed: bytes.readUInt32LE(local + 18), localExpanded: bytes.readUInt32LE(local + 22),
      localNameLength, localNameHex: localNameBytes.toString('hex'), localExtraLength, localHeaderLength, localRecordLength: intervalEnd - local, centralRecordLength,
      descriptorSignature: bytes.readUInt32LE(descriptorOffset), descriptorCrc: bytes.readUInt32LE(descriptorOffset + 4), descriptorCompressed: bytes.readUInt32LE(descriptorOffset + 8), descriptorExpanded: bytes.readUInt32LE(descriptorOffset + 12),
      descriptorLength: 16, descriptorHex: bytes.subarray(descriptorOffset, intervalEnd).toString('hex'), payloadSha256: sha256(payload), payloadBase64: payload.toString('base64'),
      localOffset: local, dataStart, descriptorOffset, intervalEnd, centralOffset: cursor, sequenceIndex}
    assert.equal(record.method, 0); assert.equal(compressed, record.expanded, 'STORE compressed/expanded equality')
    assert.equal(record.descriptorSignature, 0x08074b50)
    assert.equal(record.crc, crc32(payload), 'Independent payload CRC32')
    assert.equal(record.descriptorCrc, record.crc); assert.equal(record.descriptorCompressed, compressed); assert.equal(record.descriptorExpanded, record.expanded)
    assert.deepEqual(payload, Buffer.from(input[name], 'base64'), 'Exact reviewed input payload')
    // Bind EVERY non-positional property while this is still the untouched raw
    // record; subsequent entry-keyed normalization cannot hide metadata changes.
    for (const [field, expected] of Object.entries(reviewedEntryProfile(name, input[name])))
      assert.deepEqual(record[field], expected, `Exact reviewed field ${name}:${field}`)
    inventory.push(record); expectedLocalStart = intervalEnd; cursor += centralRecordLength
  }
  assert.equal(expectedLocalStart, centralStart, 'Local header/name/payload/descriptor intervals exactly cover [0, centralStart)')
  assert.equal(cursor, eocd, 'Central records exactly cover their declared interval')
  assert.deepEqual([...seenNames].sort(), expectedNames)
  // All ranges and bytes have now been validated. Remove absolute positions and
  // sequence indexes ONLY; retain every non-positional field and relative length.
  const canonical = {archive: {length: bytes.length, disk, centralDisk, entriesOnDisk, entryCount: count,
    localAreaLength: centralStart, centralSize, eocdLength: 22, commentLength}, entries: inventory.map(record => {
      const {localOffset, dataStart, descriptorOffset, intervalEnd, centralOffset, sequenceIndex, ...entry} = record
      return entry
    }).sort((left, right) => left.name < right.name ? -1 : left.name > right.name ? 1 : 0)}
  assert.deepEqual(canonical, reviewedProfile(golden), 'Every exact metadata/payload property must match the independently reviewed golden')
  return {inventory, canonical}
}
function producerInventory(bytes, input) { return validateProducerZip(bytes, input).inventory }
function verifyRawExemplar(includeHtml) {
  assert.equal(reviewedCapture.reviewSha256, '88b1485818713b8c175a1fc4b198b8b111dea62b289d7c6356ce2de08859fcca')
  assert.equal(reviewedCapture.diagnosisSha256, '1105295401b422d13659c971f7550d2f2aeb3b365662549d13e7c97dda07811f')
  const golden = reviewedRawFixtures[includeHtml ? 1 : 0], bytes = Buffer.from(golden.base64, 'base64')
  assert.equal(bytes.length, golden.length); assert.equal(sha256(bytes), golden.sha256, 'Immutable ORIGINAL capture SHA')
  const validated = validateProducerZip(bytes, golden.input)
  return {golden, ...validated}
}
async function generateProducerFixture(includeHtml) {
  const producer = await pinnedProducer()
  const root = await validTree(includeHtml)
  try {
    const names = ['bundle.sha256', ...(includeHtml ? ['club-elo/source.html'] : []), 'manifest.json']
    const fixedDate = new Date('2024-01-01T00:00:00.000Z')
    const input = {}
    for (const name of names) {
      const file = path.join(root, name)
      await fs.chmod(file, 0o644); await fs.utimes(file, fixedDate, fixedDate)
      input[name] = (await fs.readFile(file)).toString('base64')
    }
    const specification = producer.getUploadZipSpecification(names.map(name => path.join(root, name)), root)
    const zip = await producer.createZipUploadStream(specification, 0)
    const chunks = []; let size = 0
    for await (const chunk of zip) { size += chunk.length; assert.ok(size <= 4096, 'Synthetic fixture must remain tiny'); chunks.push(chunk) }
    const bytes = Buffer.concat(chunks), inventory = producerInventory(bytes, input)
    assert.deepEqual(inventory.map(entry => entry.name).sort(), names)
    for (const entry of inventory) assert.equal(entry.payloadSha256, sha256(Buffer.from(input[entry.name], 'base64')))
    return {provenance: {...producerProof, generatorSha256: sha256(await fs.readFile(fileURLToPath(import.meta.url))), node: process.version, platform: process.platform, arch: process.arch, transitivePackageSha256: producer.transitiveSources, rawLockSha256: producer.rawLockSha256,
      inputTimestamp: fixedDate.toISOString(), inputMode: '0644', compressionLevel: 0}, input, inventory, sha256: sha256(bytes), base64: bytes.toString('base64')}
  } finally { await clean(root) }
}

// Explicit offline generation only after root leases Node-24 acquisition. This mode
// prints evidence for CT constants; it never edits a golden, installs, or uploads.
// node context-source-artifact.test.mjs --w2r-producer-fixture
// The immutable captures above retain the original generator SHA. New fixture mode
// output keeps untouched raw bytes/order and additionally records THIS generator SHA.
if (process.argv.includes('--w2r-producer-fixture')) {
  const fixtures = [await generateProducerFixture(false), await generateProducerFixture(true)]
  process.stdout.write(`W2R_PRODUCER_FIXTURES=${JSON.stringify(fixtures)}\n`)
} else {
  for (const includeHtml of [false, true]) {
    test(`Z pinned producer repeats reviewed ${includeHtml ? 'three' : 'two'}-file grammar and payloads across permitted entry orders`, async t => {
      const exemplar = verifyRawExemplar(includeHtml)
      const first = await generateProducerFixture(includeHtml), second = await generateProducerFixture(includeHtml)
      const firstValidated = validateProducerZip(Buffer.from(first.base64, 'base64'), first.input)
      const secondValidated = validateProducerZip(Buffer.from(second.base64, 'base64'), second.input)
      t.diagnostic(JSON.stringify({originalGeneratorSha256: reviewedCapture.originalGeneratorSha256,
        currentGeneratorSha256: first.provenance.generatorSha256, node: first.provenance.node, platform: first.provenance.platform,
        rawExemplar: {sha256: exemplar.golden.sha256, order: exemplar.inventory.map(entry => entry.name)},
        first: {sha256: first.sha256, order: first.inventory.map(entry => entry.name)},
        second: {sha256: second.sha256, order: second.inventory.map(entry => entry.name)}}))
      assert.deepEqual(firstValidated.canonical, exemplar.canonical)
      assert.deepEqual(secondValidated.canonical, exemplar.canonical)
      assert.deepEqual(firstValidated.canonical, secondValidated.canonical)
      t.diagnostic('Normalized metadata and payloads match the independently reviewed golden')
    })
  }
}
