const fs=require('fs');
const root=String.raw`C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\src\OpenFill.Core\Browser\WhatsAppBrowser__v0.1__2026-10-29_2310.cs`;
const cs=fs.readFileSync(root,'utf8');
const m=cs.match(/private const string VisibleScript = """([\s\S]*?)""";/);
if(!m)throw Error('VISIBLE_SCRIPT_NOT_FOUND');
const body={innerText:'OpenFill D01 QA 2026-11-05 self-test 01',textContent:'OpenFill D01 QA 2026-11-05 self-test 01'};
const message={
 innerText:'OpenFill D01 QA 2026-11-05 self-test 01\n08:20',
 getAttribute(name){return name==='data-id'?'3EB001360652CD21E97F52':null;},
 closest(sel){return sel.includes('msg-container')||sel==='.message-out'?this:null;},
 matches(sel){return sel==='.message-out';},
 querySelector(sel){return sel.includes('.selectable-text')?body:null;},
 querySelectorAll(sel){return sel==='.selectable-text'?[body]:[];}
};
const main={
 querySelectorAll(sel){return sel.includes('msg-container')?[message]:[];},
 querySelector(sel){return sel.includes('contenteditable')?{}:null}
};
const pane={querySelectorAll(){return []}};
const document={querySelector(sel){return sel==='#main'?main:sel==='#pane-side'?pane:null;}};
const view=new Function('document','return ('+m[1].trim()+')')(document);
let checks=0;
function check(x,msg){if(!x)throw Error('D01_BODY_'+msg);checks++;}
check(view.messages.length===1,'ONE');
check(view.messages[0].id==='3EB001360652CD21E97F52','ID');
check(view.messages[0].direction==='out','OUT');
check(view.messages[0].text==='OpenFill D01 QA 2026-11-05 self-test 01','EXACT');
check(!view.messages[0].text.includes('08:20'),'NO_TIMESTAMP');
console.log('D01_WHATSAPP_BODY_'+checks+'_CHECKS_OK');