const fs = require('fs');
const path = String.raw`C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\src\OpenFill.Core\Agent\ToolRegistry__v0.22__2026-10-29_2314.cs`;
const source = fs.readFileSync(path, 'utf8');
const part = source.split('Name = "act_locator"')[1].split('Name = "verify_view"')[0];
const match = part.match(/var js = """([\s\S]*?)"""\.Replace\("__HOST__"/);
if (!match) throw new Error('ACT_LOCATOR_EMBEDDED_JS_NOT_FOUND');
let clickCount = 0;
global.location = {hostname: '127.0.0.1'};
const target = {innerText:'Other person +48 555 000 111',textContent:'Other person +48 555 000 111',disabled:false,click(){clickCount++;}};
global.document = {querySelectorAll(selector){return selector === '#entry' ? [target] : [];}};
function run(expected, host='127.0.0.1') {
 let script = match[1];
 for (const [key,value] of Object.entries({'__HOST__':host,'__SELECTOR__':'#entry','__VERB__':'click','__VALUE__':'','__EXPECTED__':expected})) script=script.replaceAll(key,JSON.stringify(value));
 return eval(script);
}
let n=0;
function ok(cond, name){if(!cond) throw new Error('FAIL '+name); n++;}
const mismatch=run('+48 575 898 178 (You)');
ok(mismatch.reason_code==='target_text_mismatch','reason');
ok(mismatch.action_attempted===false && clickCount===0,'no action');
ok(mismatch.observed_text.includes('Other person'),'observed');
ok(mismatch.observed_count===1 && mismatch.observed_text.length<=180,'bounds');
const good=run('Other person');
ok(good.ok===true && clickCount===1,'good click');
const mismatch2=run('Unknown');
ok(mismatch2.action_attempted===false && clickCount===1,'no retry effect');
const wrongHost=run('Other person','malicious.invalid');
ok(wrongHost.reason_code==='host_mismatch' && clickCount===1,'host');
console.log('E03_ACT_LOCATOR_EMBEDDED_JS_'+n+'_CHECKS_OK');
