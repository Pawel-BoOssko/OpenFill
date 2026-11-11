"""Generate three realistic self-contained local SPA fixtures. Nothing is sent externally."""
from pathlib import Path
ROOT=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\tests\realistic_sites")
ROOT.mkdir(parents=True,exist_ok=True)
css="""<style>
*{box-sizing:border-box}body{margin:0;font:14px Segoe UI,Arial;background:#eef2f5;color:#17212b}
header{height:60px;background:#fff;display:flex;align-items:center;padding:0 25px;gap:32px;border-bottom:1px solid #ddd}
.brand{font-size:21px;font-weight:700;color:#1866a6}input,textarea,select{font:inherit;padding:9px;border:1px solid #b8c6d3;border-radius:6px}
button{font:inherit;padding:8px 13px;border:1px solid #a7bdcd;background:white;border-radius:7px;cursor:pointer}
button:hover{background:#e5f1ff}.shell{display:flex;height:calc(100vh - 60px);max-width:1480px;margin:auto;background:white}
aside{width:395px;border-right:1px solid #dde5ea;display:flex;flex-direction:column;min-height:0}
main{flex:1;display:flex;flex-direction:column;min-width:0;min-height:0}
.toolbar{padding:16px;border-bottom:1px solid #ddd;display:flex;gap:8px;align-items:center}
.scroll{overflow:auto;min-height:0;flex:1}.item{padding:12px 14px;border-bottom:1px solid #eff1f3;min-height:64px;cursor:pointer}
.item:hover{background:#e8f3ff}.item.active{background:#d9eefb}.name{font-weight:650}.preview{color:#576a78;font-size:12px;margin-top:5px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.title{font-size:17px;font-weight:600;margin-right:auto}.chat{padding:24px;display:flex;flex-direction:column;gap:9px}
.bubble{max-width:65%;padding:10px 14px;border-radius:12px;background:#fff;box-shadow:0 1px 3px #0001}
.bubble.out{align-self:flex-end;background:#d5f6df}.composer{display:flex;gap:8px;padding:14px;background:#f8fafb}
.small{font-size:12px;color:#647280}.pill{background:#d9ebff;color:#1761a0;border-radius:20px;padding:3px 9px;font-size:12px}
table{border-collapse:collapse;width:100%}th,td{text-align:left;padding:12px 15px;border-bottom:1px solid #e4eaef}
th{background:#f1f5f8;position:sticky;top:0}.card{padding:22px;background:white;border-bottom:1px solid #e8eef1}
</style>"""
def html(title,body,script):
    return "<!doctype html><html lang='pl'><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'><title>"+title+"</title>"+css+"</head><body>"+body+"<script>"+script+"</script></body></html>"
inbox_body="""<header><div class="brand">Conversations</div><nav role="navigation" aria-label="Main navigation"><button>Updates</button><button>Contacts</button><button>Settings</button></nav><span class="pill" id="sent-count">Sent 0</span></header>
<div class="shell"><aside><div class="toolbar"><strong class="title">Chats (180)</strong><button id="new-chat">New chat</button></div>
<div class="toolbar"><input aria-label="Search conversations" id="search" placeholder="Search people and messages" style="width:100%"></div>
<div id="conversation-scroll" class="scroll"><div id="conversation-list" role="list" aria-label="Visible conversations"></div></div></aside>
<main><div class="toolbar"><span class="title" id="contact-name">Select conversation</span><button>Details</button><button>Search in chat</button></div>
<div class="scroll" id="history-scroll"><div class="chat" role="log" aria-label="Conversation history" id="thread"></div></div>
<div class="composer"><input aria-label="Write message" id="composer" placeholder="Type a message" style="flex:1"><button id="send">Send</button></div></main></div>"""
inbox_script="""
const people=Array.from({length:180},(_,i)=>({id:i,name:['Anna','Jan','Kasia','Marek','Olivia','Ravi','Joanna','Tomasz'][i%8]+' '+['Nowak','Green','Kowalska','Adebayo','Jones','Miller'][Math.floor(i/8)%6]+' '+(i+1),preview:['Thanks for the update','Can we schedule tomorrow?','Contract details received','I will reply shortly'][i%4],unread:i%7===0}));
let selected=0, sent=0, filtered=people.slice();
const list=document.getElementById('conversation-list'), scroller=document.getElementById('conversation-scroll');
function listRender(){let first=Math.max(0,Math.floor(scroller.scrollTop/66));list.innerHTML='<div style="height:'+first*66+'px"></div>';
for(const p of filtered.slice(first,first+24)){const row=document.createElement('div');row.className='item'+(p.id===selected?' active':'');row.setAttribute('role','listitem');row.setAttribute('data-thread-id',String(p.id));row.innerHTML='<span class="name">'+p.name+'</span> <span class="small">13:4'+p.id%10+'</span><div class="preview">'+p.preview+'</div>';row.onclick=()=>openThread(p.id);list.appendChild(row)}
const bottom=Math.max(0,filtered.length-first-24)*66;list.insertAdjacentHTML('beforeend','<div style="height:'+bottom+'px"></div>')}
function openThread(id){selected=id;const p=people[id];document.getElementById('contact-name').textContent=p.name;const thread=document.getElementById('thread');thread.innerHTML='';
for(let i=0;i<15;i++){const msg=document.createElement('article');msg.className='bubble'+(i%2?' out':'');msg.setAttribute('role','listitem');msg.setAttribute('data-message-id','m-'+id+'-'+i);msg.textContent=(i%2?'You: ':'Contact: ')+['Following up on the quotation','Please send availability','I have attached the overview','Can we discuss next steps?'][i%4]+' #'+(id+1);thread.appendChild(msg)}listRender()}
document.getElementById('search').oninput=e=>{filtered=people.filter(p=>p.name.toLowerCase().includes(e.target.value.toLowerCase()));scroller.scrollTop=0;listRender()};
scroller.onscroll=listRender;
document.getElementById('send').onclick=()=>{const inp=document.getElementById('composer');if(!inp.value.trim())return;const msg=document.createElement('article');msg.className='bubble out';msg.setAttribute('role','listitem');msg.setAttribute('data-message-id','new-'+(++sent));msg.textContent=inp.value;document.getElementById('thread').appendChild(msg);people[selected].preview=inp.value;inp.value='';document.getElementById('sent-count').textContent='Sent '+sent;listRender()};
document.getElementById('composer').onkeydown=e=>{if(e.key==='Enter')document.getElementById('send').click()};
openThread(0);
"""
(ROOT/'messenger.html').write_text(html("Conversations | Local fixture",inbox_body,inbox_script),encoding='utf-8')
network_body="""<header><div class="brand">ProNetwork</div><nav role="navigation"><button>Home</button><button>My Network</button><button>Messages</button><button>Jobs</button></nav><span class="pill" id="sent-count">Messages sent: 0</span></header>
<div class="shell"><aside><div class="toolbar"><strong class="title">People search</strong></div>
<div class="toolbar"><input id="search" aria-label="Search people" placeholder="Name, company, title" style="width:100%"></div>
<div class="toolbar"><select id="filter" aria-label="Filter industry"><option>All sectors</option><option>Technology</option><option>Manufacturing</option><option>Finance</option></select></div>
<div id="scroll" class="scroll"><div role="list" aria-label="Search results" id="results"></div></div></aside>
<main><div class="toolbar"><span class="title" id="profile">Select a person</span><button>Connect</button><button>Follow</button></div>
<div class="card"><h2 id="headline">Professional profile</h2><p id="company">Company</p><p id="description">Current role and experience</p></div>
<div class="toolbar"><span class="title">Messages</span></div><div id="conversation" class="scroll chat" role="log" aria-label="Conversation timeline"></div>
<div class="composer"><textarea id="draft" aria-label="Write a message" rows="2" style="flex:1" placeholder="Write a message"></textarea><button id="send">Send</button></div></main></div>"""
network_script="""
const people=Array.from({length:220},(_,i)=>({id:i,name:['Jordan','Alex','Chris','Taylor','Morgan','Pat','Robin'][i%7]+' '+['Hill','Kim','Brown','Lee','Adams','Chen'][Math.floor(i/7)%6]+' #'+(i+1),company:['Northwind Industries','Contoso Systems','Fabrikam','Alpine Group'][i%4],sector:['Technology','Manufacturing','Finance'][i%3],role:['Director of Operations','Chief Technology Officer','Strategy Manager','Business Architect'][i%4]}));
let filtered=people.slice(),active=1,sent=0;const scroll=document.getElementById('scroll');
function showList(){const first=Math.floor(scroll.scrollTop/77);const root=document.getElementById('results');root.innerHTML='<div style="height:'+first*77+'px"></div>';
for(const p of filtered.slice(first,first+22)){const row=document.createElement('div');row.className='item'+(active===p.id?' active':'');row.setAttribute('role','listitem');row.setAttribute('data-profile-id','p'+p.id);row.innerHTML='<div class="name">'+p.name+'</div><div class="preview">'+p.role+' · '+p.company+'</div>';row.onclick=()=>profile(p.id);root.appendChild(row)}
root.insertAdjacentHTML('beforeend','<div style="height:'+Math.max(0,(filtered.length-first-22)*77)+'px"></div>')}
function profile(id){active=id;const p=people[id];document.getElementById('profile').textContent=p.name;document.getElementById('headline').textContent=p.role;document.getElementById('company').textContent=p.company;document.getElementById('description').textContent=p.sector+' specialist with 12 years of business transformation experience.';
const c=document.getElementById('conversation');c.innerHTML='';for(let i=0;i<6;i++){const msg=document.createElement('article');msg.className='bubble'+(i%2?' out':'');msg.setAttribute('role','listitem');msg.textContent=['Hello, nice to connect','Thanks for reaching out','Are you available next week?'][i%3];c.appendChild(msg)}showList()}
function apply(){const q=document.getElementById('search').value.toLowerCase(),industry=document.getElementById('filter').value;filtered=people.filter(p=>(p.name+' '+p.company+' '+p.role).toLowerCase().includes(q)&&(industry==='All sectors'||p.sector===industry));scroll.scrollTop=0;showList()}
document.getElementById('search').oninput=apply;document.getElementById('filter').onchange=apply;scroll.onscroll=showList;
document.getElementById('send').onclick=()=>{const draft=document.getElementById('draft');if(!draft.value.trim())return;const b=document.createElement('article');b.className='bubble out';b.setAttribute('role','listitem');b.textContent=draft.value;document.getElementById('conversation').appendChild(b);sent++;document.getElementById('sent-count').textContent='Messages sent: '+sent;draft.value=''};
profile(1);
"""
(ROOT/'network.html').write_text(html("People | ProNetwork fixture",network_body,network_script),encoding='utf-8')
crm_body="""<header><div class="brand">Operations Desk</div><nav role="navigation"><button>Overview</button><button>Pipeline</button><button>Reports</button></nav><span class="pill" id="edited">Changed: 0</span></header>
<section style="padding:22px;max-width:1480px;margin:auto;background:#fff"><h2>Accounts and opportunities</h2>
<div class="toolbar"><input id="query" aria-label="Search accounts" placeholder="Find company or owner"><select id="state" aria-label="Filter status"><option>All</option><option>Open</option><option>Won</option><option>Review</option></select><button id="export">Export</button></div>
<div style="max-height:68vh;overflow:auto"><table aria-label="Opportunity table"><thead><tr><th>Company</th><th>Owner</th><th>Status</th><th>Value</th><th>Action</th></tr></thead><tbody id="rows"></tbody></table></div>
<div class="toolbar"><button id="prev">Previous</button><span id="page">Page 1</span><button id="next">Next</button></div></section>
<div id="modal" style="display:none;position:fixed;left:25%;top:16%;width:50%;background:white;border:1px solid #abb;padding:24px;box-shadow:0 5px 25px #0004">
<h3>Edit opportunity</h3><form id="edit"><label>Company <input id="company" required></label><label>Status <select id="status"><option>Open</option><option>Won</option><option>Review</option></select></label>
<label>Value <input type="number" id="amount" required></label><button type="submit">Save changes</button><button type="button" id="cancel">Cancel</button></form></div>"""
crm_script="""
const data=Array.from({length:190},(_,i)=>({id:i,company:['Contoso','Northwind','Alpine','Woodgrove','Fabrikam'][i%5]+' '+(i+1),owner:['Anna','Jordan','Lee','Ravi'][i%4],status:['Open','Review','Won'][i%3],value:1000+i*157}));
let page=0,selection=null,changed=0;
function refresh(){const q=document.getElementById('query').value.toLowerCase(),st=document.getElementById('state').value;const match=data.filter(x=>(x.company+' '+x.owner).toLowerCase().includes(q)&&(st==='All'||st===x.status));const chunk=match.slice(page*25,(page+1)*25);document.getElementById('rows').innerHTML=chunk.map(x=>'<tr role="row"><td>'+x.company+'</td><td>'+x.owner+'</td><td>'+x.status+'</td><td>$'+x.value+'</td><td><button data-id="'+x.id+'">Edit</button></td></tr>').join('');document.querySelectorAll('#rows button').forEach(b=>b.onclick=()=>edit(Number(b.dataset.id)));document.getElementById('page').textContent='Page '+(page+1)+' of '+Math.ceil(match.length/25)}
function edit(id){selection=id;const x=data[id];document.getElementById('company').value=x.company;document.getElementById('status').value=x.status;document.getElementById('amount').value=x.value;document.getElementById('modal').style.display='block'}
document.getElementById('edit').onsubmit=e=>{e.preventDefault();if(selection===null)return;const x=data[selection];x.company=document.getElementById('company').value;x.status=document.getElementById('status').value;x.value=Number(document.getElementById('amount').value);changed++;document.getElementById('edited').textContent='Changed: '+changed;document.getElementById('modal').style.display='none';refresh()};
document.getElementById('cancel').onclick=()=>document.getElementById('modal').style.display='none';
document.getElementById('next').onclick=()=>{page++;refresh()};document.getElementById('prev').onclick=()=>{page=Math.max(0,page-1);refresh()};
document.getElementById('query').oninput=()=>{page=0;refresh()};document.getElementById('state').onchange=()=>{page=0;refresh()};refresh();
"""
(ROOT/'crm.html').write_text(html("Operations Desk | Local fixture",crm_body,crm_script),encoding='utf-8')
for file in ROOT.glob('*.html'):print(file.name,file.stat().st_size)
