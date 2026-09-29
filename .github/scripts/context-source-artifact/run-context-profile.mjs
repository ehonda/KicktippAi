import {spawn} from 'node:child_process'
import path from 'node:path'
import {fileURLToPath} from 'node:url'
import {readdir,readFile,open} from 'node:fs/promises'
import {randomUUID} from 'node:crypto'

export const consumers = ['pes-squad-context','schadensfresse-context','relaxdays-tippt-context','arena-sol-xhigh-context','arena-sol-high-context','arena-luna-medium-context','arena-terra-xhigh-context','arena-luna-none-context']
const max = 9223372036854775807n
function canonicalId(value) { return /^[1-9][0-9]{0,18}$/.test(value ?? '') && BigInt(value) <= max }
export function profileArguments(environment) {
  const input = name => environment[`INPUT_${name.toUpperCase()}`] ?? ''
  const community = input('community-context'), scope = input('scope')
  const args = ['run','--no-build','--project','src/Orchestrator/Orchestrator.csproj','--configuration','Release','--','collect-context','profile','--community-context',community,'--competition','bundesliga-2026-27','--enable-club-elo-source','--context-source-only','--context-source-scope',scope]
  if (scope === 'development') {
    if (community !== 'ehonda-dev-buli-2627' || ['cycle-id','current-lane','producer-lane','consumers'].some(name => input(name))) throw new Error('CONTEXT_PROFILE_AUTHORITY_INVALID')
  } else if (scope === 'production-live') {
    const lane = input('current-lane'), index = consumers.indexOf(lane)
    const expectedCommunity = index === 0 ? 'pes-squad' : index === 1 ? 'schadensfresse' : index === 2 ? 'relaxdays-tippt' : 'ehonda-ai-arena'
    if (environment.GITHUB_REPOSITORY !== 'ehonda/KicktippAi' || !canonicalId(environment.GITHUB_REPOSITORY_ID) || !canonicalId(environment.GITHUB_RUN_ID)
      || input('cycle-id') !== `gha:${environment.GITHUB_REPOSITORY_ID}:${environment.GITHUB_RUN_ID}`
      || index < 0 || community !== expectedCommunity || input('producer-lane') !== consumers[0] || input('consumers') !== consumers.join(',')) throw new Error('CONTEXT_PROFILE_AUTHORITY_INVALID')
    for (const name of ['cycle-id','current-lane','producer-lane','consumers']) args.push(`--context-source-${name === 'current-lane' ? 'current-lane' : name === 'producer-lane' ? 'producer-lane' : name}`,input(name))
  } else throw new Error('CONTEXT_PROFILE_AUTHORITY_INVALID')
  return args
}
export async function runProfile(environment = process.env, execute = executeBounded) {
  const args = profileArguments(environment) // admission precedes npm, .NET, and bridge initialization
  const cwd = environment.GITHUB_WORKSPACE
  if (!path.isAbsolute(cwd ?? '')) throw new Error('CONTEXT_PROFILE_WORKSPACE_INVALID')
  const packageDirectory = path.join(cwd,'.github','scripts','context-source-artifact')
  return withCancellation(async signal => {
    await execute('npm',['ci','--ignore-scripts'],packageDirectory,environment,60000,signal)
    await execute('dotnet',args,cwd,environment,220000,signal)
  })
}
function failure(code, signal) { return Object.assign(new Error(code),{code,signal}) }
export async function withCancellation(action) {
  const controller = new AbortController()
  const interrupt = () => controller.abort(failure('CONTEXT_PROFILE_CANCELLED','SIGINT'))
  const terminate = () => controller.abort(failure('CONTEXT_PROFILE_CANCELLED','SIGTERM'))
  process.on('SIGINT',interrupt)
  process.on('SIGTERM',terminate)
  try { return await action(controller.signal) }
  finally { process.off('SIGINT',interrupt); process.off('SIGTERM',terminate) }
}
async function terminateTree(child,owner) {
  if (!child.pid) return
  if (process.platform !== 'win32') {
    if (process.platform === 'linux') {
      const owned = await freezeLinuxTree(owner)
      for (const processIdentity of owned.values()) await signalLinuxIdentity(processIdentity,'SIGTERM')
      for (const processIdentity of owned.values()) await signalLinuxIdentity(processIdentity,'SIGCONT')
      await new Promise(resolve => setTimeout(resolve,250))
      for (const processIdentity of [...owned.values()].reverse()) await signalLinuxIdentity(processIdentity,'SIGKILL')
      const deadline = Date.now()+2000
      while (true) {
        const remaining = await Promise.all([...owned.values()].map(async identity => {
          const current = await linuxIdentity(identity.pid)
          return current && current.started === identity.started && current.state !== 'Z'
        }))
        if (!remaining.some(Boolean)) break
        if (Date.now() >= deadline) throw failure('CONTEXT_PROFILE_CLEANUP_FAILED')
        await new Promise(resolve => setTimeout(resolve,25))
      }
      return
    }
    try { process.kill(-child.pid,'SIGTERM') } catch (error) { if (error.code !== 'ESRCH') throw error }
    // Give cooperative children a bounded chance to reap descendants, then enforce termination.
    await new Promise(resolve => setTimeout(resolve,250))
    try { process.kill(-child.pid,'SIGKILL') } catch (error) { if (error.code !== 'ESRCH') throw error }
    return
  }
  // taskkill /T owns only the tree rooted at our child; await the tree killer too.
  await new Promise((resolve,reject) => {
    const killer = spawn('taskkill.exe',['/PID',String(child.pid),'/T','/F'],{shell:false,stdio:'ignore',windowsHide:true})
    let expired = false
    const timer = setTimeout(() => { expired = true; killer.kill(); child.kill() },10000)
    killer.once('error',error => { clearTimeout(timer); reject(error) })
    killer.once('close',code => { clearTimeout(timer); if (!expired && code === 0) resolve(); else reject(failure('CONTEXT_PROFILE_CLEANUP_FAILED')) })
  })
}
async function linuxIdentity(pid) {
  try {
    const stat = await readFile(`/proc/${pid}/stat`,'utf8')
    const fields = stat.slice(stat.lastIndexOf(')')+2).trim().split(/\s+/)
    return {pid,parent:Number(fields[1]),started:fields[19],state:fields[0]}
  } catch(error) { if (error.code === 'ENOENT' || error.code === 'ESRCH') return null; throw error }
}
async function signalLinuxIdentity(identity,signal) {
  const current = await linuxIdentity(identity.pid)
  if (!current || current.started !== identity.started || current.state === 'Z') return
  try { process.kill(identity.pid,signal) } catch(error) { if(error.code !== 'ESRCH')throw error }
}
const ownerVariable = 'KICKTIPPAI_CONTEXT_SOURCE_PROCESS_OWNER'
async function linuxOwnedSnapshot(owner,deadline) {
  const marker = Buffer.from(`${ownerVariable}=${owner}\0`)
  const buffer = Buffer.alloc(2*1024*1024)
  const pids = (await readdir('/proc')).filter(value => /^[1-9][0-9]*$/.test(value))
  if (pids.length > 8192) throw failure('CONTEXT_PROFILE_CLEANUP_FAILED')
  const owned = []
  for (const pid of pids) {
    if (Date.now() >= deadline) throw failure('CONTEXT_PROFILE_CLEANUP_FAILED')
    const before = await linuxIdentity(Number(pid))
    if (!before || before.state === 'Z') continue
    let file
    try {
      file = await open(`/proc/${pid}/environ`,'r')
      const {bytesRead} = await file.read(buffer,0,buffer.length,0)
      const offset = buffer.subarray(0,bytesRead).indexOf(marker)
      if (offset < 0 || (offset > 0 && buffer[offset-1] !== 0)) continue
      const after = await linuxIdentity(Number(pid))
      if (after && after.started === before.started && after.state !== 'Z') owned.push(after)
    } catch(error) {
      // Protected or vanished processes are never signalled. No environment bytes are logged.
      if (!['ENOENT','ESRCH','EACCES','EPERM'].includes(error.code)) throw error
    } finally { await file?.close() }
  }
  return owned
}
async function freezeLinuxTree(owner) {
  const owned = new Map()
  try {
    // A child-only nonce is inherited by the artifact launcher's full environment.
    // It survives reparenting and separate groups without depending on the root PID.
    // Freeze discoveries before resampling, so each stopped process cannot fork again.
    const deadline = Date.now()+5000
    for (let pass=0;pass<16 && Date.now()<deadline;pass++) {
      const snapshot = await linuxOwnedSnapshot(owner,deadline)
      let added = false
      for (const identity of snapshot) if (!owned.has(identity.pid)) {
          if (owned.size >= 4096) throw failure('CONTEXT_PROFILE_CLEANUP_FAILED')
          owned.set(identity.pid,identity);await signalLinuxIdentity(identity,'SIGSTOP');added = true
      }
      if (!added) return owned
    }
    throw failure('CONTEXT_PROFILE_CLEANUP_FAILED')
  } catch(error) {
    // Never leave a frozen owned process when observation fails.
    for (const identity of [...owned.values()].reverse()) await signalLinuxIdentity(identity,'SIGKILL')
    throw error
  }
}
export async function executeBounded(command,args,cwd,environment,timeout,signal) {
  signal?.throwIfAborted()
  const owner = process.platform === 'linux' ? randomUUID() : undefined
  const childEnvironment = owner ? {[ownerVariable]:owner,...environment,[ownerVariable]:owner} : environment
  const child = spawn(command,args,{cwd,env:childEnvironment,shell:false,stdio:'inherit',detached:process.platform !== 'win32',windowsHide:true})
  let stopReason, stopping
  let cleanupFailed
  const cleanupFailure = new Promise((_,reject) => { cleanupFailed = reject })
  // close certifies only the direct child. Linux ownership cleanup below is independent.
  const closed = new Promise((resolve,reject) => {
    child.once('error',() => reject(failure('CONTEXT_PROFILE_EXECUTION_FAILED')))
    child.once('close',(code,childSignal) => resolve({code,signal:childSignal}))
  })
  const stop = reason => {
    stopReason ??= reason
    if (!stopping) { stopping = terminateTree(child,owner); stopping.catch(cleanupFailed) }
  }
  const aborted = () => stop(signal.reason ?? failure('CONTEXT_PROFILE_CANCELLED'))
  signal?.addEventListener('abort',aborted,{once:true})
  const timer = setTimeout(() => stop(failure('CONTEXT_PROFILE_DEADLINE_EXCEEDED')),timeout)
  let result
  try {
    if (signal?.aborted) aborted()
    result = await Promise.race([closed,cleanupFailure])
  } finally {
    try {
      // Always stop nonce-owned work before returning success or failure, even if the
      // parent already exited and a detached bridge descendant was reparented.
      if (owner && !stopping) stopping = terminateTree(child,owner)
      if (stopping) await stopping
    } finally {
      clearTimeout(timer)
      signal?.removeEventListener('abort',aborted)
    }
  }
  if (stopReason) throw stopReason
  if (result.signal || result.code !== 0) throw failure('CONTEXT_PROFILE_EXECUTION_FAILED')
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { await runProfile() } catch (error) {
    process.stderr.write(`::error::${error.code === 'CONTEXT_PROFILE_CANCELLED' ? 'CONTEXT_PROFILE_CANCELLED' : 'CONTEXT_PROFILE_FAILED'}\n`)
    process.exitCode = error.signal === 'SIGINT' ? 130 : error.signal === 'SIGTERM' ? 143 : 1
  }
}
