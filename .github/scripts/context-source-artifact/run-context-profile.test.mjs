import {test} from 'node:test'
import assert from 'node:assert/strict'
import {spawn} from 'node:child_process'
import {mkdtemp,readFile,writeFile,rm} from 'node:fs/promises'
import os from 'node:os'
import path from 'node:path'
import {profileArguments,runProfile,consumers} from './run-context-profile.mjs'
const dev = {GITHUB_WORKSPACE:process.cwd(),'INPUT_COMMUNITY-CONTEXT':'ehonda-dev-buli-2627',INPUT_SCOPE:'development'}
const production = {...dev,GITHUB_REPOSITORY:'ehonda/KicktippAi',GITHUB_REPOSITORY_ID:'9007199254740993',GITHUB_RUN_ID:'9007199254740995','INPUT_COMMUNITY-CONTEXT':'pes-squad',INPUT_SCOPE:'production-live','INPUT_CYCLE-ID':'gha:9007199254740993:9007199254740995','INPUT_CURRENT-LANE':consumers[0],'INPUT_PRODUCER-LANE':consumers[0],INPUT_CONSUMERS:consumers.join(',')}
test('development runs source-only without cycle overrides or credentials',() => {
 const args=profileArguments(dev); assert.ok(args.includes('--context-source-only')); assert.ok(args.includes('--enable-club-elo-source')); assert.ok(!args.includes('--kicktipp-credential-profile')); assert.ok(!args.includes('--context-source-cycle-id'))
})
test('every production lane retains exact bigint cycle and full authority',() => {
 for (const [index,lane] of consumers.entries()) { const community=index===0?'pes-squad':index===1?'schadensfresse':index===2?'relaxdays-tippt':'ehonda-ai-arena'; assert.ok(profileArguments({...production,'INPUT_CURRENT-LANE':lane,'INPUT_COMMUNITY-CONTEXT':community}).includes(production['INPUT_CYCLE-ID'])) }
})
test('invalid identity and authority fail before any process execution',async() => {
 for (const override of [{'GITHUB_RUN_ID':'09007199254740995'},{'INPUT_CYCLE-ID':'gha:1:2'},{INPUT_CONSUMERS:consumers.slice(0,1).join(',')},{'INPUT_PRODUCER-LANE':consumers[1]},{'INPUT_COMMUNITY-CONTEXT':'other'},{INPUT_SCOPE:'wrong'}]) {
  let calls=0; await assert.rejects(runProfile({...production,...override},async()=>{calls++})); assert.equal(calls,0)
 }
})
test('locked install precedes source-only execution and propagates failures',async() => {
 const calls=[]; await runProfile(dev,async(...args)=>calls.push(args)); assert.equal(calls[0][0],'npm'); assert.deepEqual(calls[0][1],['ci','--ignore-scripts']); assert.equal(calls[1][0],'dotnet')
 for(const failed of ['npm','dotnet']) { await assert.rejects(runProfile(dev,async(command)=>{if(command===failed)throw Error('failed')})) }
})

async function eventually(predicate) {
 const end=Date.now()+5000
 while(Date.now()<end) { if(await predicate()) return; await new Promise(resolve=>setTimeout(resolve,25)) }
 throw Error('bounded subprocess observation expired')
}
async function alive(pid) {
 try { process.kill(pid,0) } catch(error) { if(error.code==='ESRCH')return false; throw error }
 if(process.platform==='linux') { try { const stat=await readFile(`/proc/${pid}/stat`,'utf8'); if(stat.slice(stat.lastIndexOf(')')+2).startsWith('Z'))return false } catch(error) { if(error.code==='ENOENT')return false; throw error } }
 return true
}
for(const mode of ['SIGINT','SIGTERM','deadline']) {
 test(`actual wrapper ${mode} terminates and awaits owned child and grandchild`,{timeout:15000,skip:process.platform==='win32'&&mode!=='deadline'},async() => {
  const directory=await mkdtemp(path.join(os.tmpdir(),'context-wrapper-'))
  const pidsFile=path.join(directory,'pids.json'), childFile=path.join(directory,'child.mjs'), wrapperFile=path.join(directory,'wrapper.mjs')
  const moduleUrl=new URL('./run-context-profile.mjs',import.meta.url).href
  await writeFile(childFile,`import {spawn} from 'node:child_process'; import {writeFileSync} from 'node:fs';
   const grandchild=spawn(process.execPath,['-e','setInterval(()=>{},1000)'],{stdio:'inherit',detached:process.platform!=='win32',windowsHide:true});
   process.on('SIGTERM',()=>{grandchild.kill('SIGTERM'); grandchild.once('close',()=>process.exit(0))});
   writeFileSync(${JSON.stringify(pidsFile)},JSON.stringify([process.pid,grandchild.pid])); setInterval(()=>{},1000);`)
  await writeFile(wrapperFile,`import {executeBounded,withCancellation} from ${JSON.stringify(moduleUrl)};
   const before=[process.listenerCount('SIGINT'),process.listenerCount('SIGTERM')];
   try {await withCancellation(signal=>executeBounded(process.execPath,[${JSON.stringify(childFile)}],${JSON.stringify(directory)},process.env,${mode==='deadline'?800:8000},signal)); process.exitCode=99}
   catch(error){console.log(error.code);process.exitCode=error.code==='CONTEXT_PROFILE_CANCELLED'?42:error.code==='CONTEXT_PROFILE_DEADLINE_EXCEEDED'?43:98}
   if(JSON.stringify(before)!==JSON.stringify([process.listenerCount('SIGINT'),process.listenerCount('SIGTERM')]))process.exitCode=97;`)
  const wrapper=spawn(process.execPath,[wrapperFile],{stdio:['ignore','pipe','pipe'],windowsHide:true})
  let output=''; wrapper.stdout.on('data',chunk=>output+=chunk);wrapper.stderr.on('data',chunk=>output+=chunk)
  const closed=new Promise(resolve=>wrapper.once('close',(code,signal)=>resolve({code,signal})))
  let pids=[]
  try {
   await eventually(async()=>{try {pids=JSON.parse(await readFile(pidsFile,'utf8')); return pids.length===2&&await alive(pids[0])&&await alive(pids[1])}catch(error){if(error.code==='ENOENT')return false;throw error}})
   if(mode!=='deadline')wrapper.kill(mode)
   let timer
   const result=await Promise.race([closed,new Promise((_,reject)=>{timer=setTimeout(()=>reject(Error('wrapper did not terminate within bound')),7000)})]).finally(()=>clearTimeout(timer))
   assert.equal(result.signal,null);assert.equal(result.code,mode==='deadline'?43:42,output)
   assert.match(output,new RegExp(mode==='deadline'?'CONTEXT_PROFILE_DEADLINE_EXCEEDED':'CONTEXT_PROFILE_CANCELLED'))
   for(const pid of pids)await eventually(async()=>!await alive(pid))
  } finally {
   // Failure cleanup is restricted to the exact PIDs created by this test.
   for(const pid of pids)if(await alive(pid)){try{process.kill(pid,'SIGKILL')}catch{}}
   if(wrapper.exitCode===null&&wrapper.signalCode===null)wrapper.kill('SIGKILL')
   await closed;await rm(directory,{recursive:true,force:true})
  }
 })
}

for(const exitCode of [0,7]) {
 test(`actual early parent exit ${exitCode} cleans reparented detached grandchild before completion`,{timeout:15000,skip:process.platform!=='linux'},async() => {
  const directory=await mkdtemp(path.join(os.tmpdir(),'context-wrapper-orphan-'))
  const pidsFile=path.join(directory,'pids.json'),readyFile=path.join(directory,'ready'),orphanFile=path.join(directory,'orphan.json')
  const grandchildFile=path.join(directory,'grandchild.mjs'),childFile=path.join(directory,'child.mjs'),wrapperFile=path.join(directory,'wrapper.mjs')
  const moduleUrl=new URL('./run-context-profile.mjs',import.meta.url).href
  await writeFile(grandchildFile,`import {writeFileSync} from 'node:fs';
   const originalParent=Number(process.argv[2]);
   process.on('SIGTERM',()=>writeFileSync(${JSON.stringify(orphanFile)},JSON.stringify({pid:process.pid,reparented:process.ppid!==originalParent})));
   writeFileSync(${JSON.stringify(readyFile)},'ready');setInterval(()=>{},1000);`)
  await writeFile(childFile,`import {spawn} from 'node:child_process';import {existsSync,writeFileSync} from 'node:fs';
   const grandchild=spawn(process.execPath,[${JSON.stringify(grandchildFile)},String(process.pid)],{stdio:'ignore',detached:true,windowsHide:true});grandchild.unref();
   writeFileSync(${JSON.stringify(pidsFile)},JSON.stringify([process.pid,grandchild.pid]));
   const timer=setInterval(()=>{if(existsSync(${JSON.stringify(readyFile)})){clearInterval(timer);process.exit(${exitCode})}},10);`)
  await writeFile(wrapperFile,`import {executeBounded,withCancellation} from ${JSON.stringify(moduleUrl)};
   const before=[process.listenerCount('SIGINT'),process.listenerCount('SIGTERM')];
   try{await withCancellation(signal=>executeBounded(process.execPath,[${JSON.stringify(childFile)}],${JSON.stringify(directory)},process.env,6000,signal));console.log('completed');process.exitCode=0}
   catch(error){console.log(error.code);process.exitCode=error.code==='CONTEXT_PROFILE_EXECUTION_FAILED'?41:98}
   if(JSON.stringify(before)!==JSON.stringify([process.listenerCount('SIGINT'),process.listenerCount('SIGTERM')]))process.exitCode=97;`)
  // Same UID and workspace, but no child-only ownership nonce: must survive cleanup.
  const sentinel=spawn(process.execPath,['-e','setInterval(()=>{},1000)'],{stdio:'ignore',windowsHide:true})
  const wrapper=spawn(process.execPath,[wrapperFile],{stdio:['ignore','pipe','pipe'],windowsHide:true})
  let output='';wrapper.stdout.on('data',chunk=>output+=chunk);wrapper.stderr.on('data',chunk=>output+=chunk)
  const closed=new Promise(resolve=>wrapper.once('close',(code,signal)=>resolve({code,signal})))
  const sentinelClosed=new Promise(resolve=>sentinel.once('close',resolve))
  let pids=[]
  try {
   await eventually(async()=>{try{pids=JSON.parse(await readFile(pidsFile,'utf8'));return pids.length===2}catch(error){if(error.code==='ENOENT')return false;throw error}})
   let timer
   const result=await Promise.race([closed,new Promise((_,reject)=>{timer=setTimeout(()=>reject(Error('early-exit cleanup exceeded bound')),7000)})]).finally(()=>clearTimeout(timer))
   assert.equal(result.signal,null);assert.equal(result.code,exitCode===0?0:41,output)
   assert.match(output,exitCode===0?/completed/:/CONTEXT_PROFILE_EXECUTION_FAILED/)
   const orphan=JSON.parse(await readFile(orphanFile,'utf8'))
   assert.equal(orphan.pid,pids[1]);assert.equal(orphan.reparented,true)
   for(const pid of pids)assert.equal(await alive(pid),false,`owned PID ${pid} still runs after wrapper completion`)
   assert.equal(await alive(sentinel.pid),true,'non-owned sentinel was terminated')
  } finally {
   for(const pid of pids)if(await alive(pid)){try{process.kill(pid,'SIGKILL')}catch{}}
   if(wrapper.exitCode===null&&wrapper.signalCode===null)wrapper.kill('SIGKILL')
   sentinel.kill('SIGKILL');await Promise.all([closed,sentinelClosed]);await rm(directory,{recursive:true,force:true})
  }
 })
}
