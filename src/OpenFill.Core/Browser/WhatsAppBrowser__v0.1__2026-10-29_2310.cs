// OpenFill - Metadata: version 0.1, 2026-10-29 23:10
// WhatsApp DOM operations used by the existing Luna; no second model, no screenshots.
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenFill.Core.Browser;

public sealed class WhatsAppBrowser
{
    private readonly BrowserController _browser;
    public WhatsAppBrowser(BrowserController browser) => _browser = browser;

    public static bool IsWhatsAppUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        return uri.Scheme == "https" && uri.Host.Equals("web.whatsapp.com", StringComparison.OrdinalIgnoreCase);
    }

    // Only explicit phone numbers or phone-type WhatsApp JIDs can be opened by this route.
    // A non-phone opaque ID needs a separately verified WhatsApp route, never list search.
    public static string? DirectUrl(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;
        var id = target.Trim();
        if (id.EndsWith("@c.us", StringComparison.OrdinalIgnoreCase))
            id = id[..^5];
        else if (id.EndsWith("@s.whatsapp.net", StringComparison.OrdinalIgnoreCase))
            id = id[..^15];
        if (id.StartsWith('+')) id = id[1..];
        if (id.Length is < 5 or > 20 || !id.All(char.IsAsciiDigit)) return null;
        return "https://web.whatsapp.com/send?phone=" + id;
    }

    private const string VisibleScript = """
(() => {
 const clean = s => String(s||'').replace(/\s+/g,' ').trim();
 const unique = x => [...new Set(x.filter(Boolean))];
 const attrs = (e,limit=80) => unique([...e.querySelectorAll('[title],[aria-label],[data-icon],[data-testid]')]
  .filter(n=>{const r=n.getBoundingClientRect();return r.height>0&&r.width>0;})
  .flatMap(n=>[n.getAttribute('title'),n.getAttribute('aria-label'),n.getAttribute('data-icon')])
  .map(clean).filter(v=>v && v.length<=limit)).slice(0,60);
 const pane = document.querySelector('#pane-side');
 let rows = pane ? [...pane.querySelectorAll('[role="row"],[data-testid="cell-frame-container"]')] : [];
 rows = rows.filter(n=>n.getClientRects().length && !rows.some(p=>p!==n && n.contains(p) && p.matches('[role="row"],[data-testid="cell-frame-container"]')));
 const seen = new Set();
 const previews = [];
 for(const node of rows) {
   const text=clean(node.innerText);
   if(!text) continue;
   const titles=attrs(node);
   const key=text+'|'+titles.join('|');
   if(seen.has(key)) continue;seen.add(key);
   previews.push({text,attributes:titles});
 }
 const main=document.querySelector('#main');
 const messageNodes=main?[...main.querySelectorAll('[data-testid="msg-container"],.message-in,.message-out')]:[];
 const messages=[];
 const used=new Set();
 for(const node of messageNodes){
   if(node.closest('[data-testid="msg-container"],.message-in,.message-out')!==node) continue;
   const text=clean(node.innerText);
   const labels=attrs(node);
   const id=clean(node.getAttribute('data-id')||node.closest('[data-id]')?.getAttribute('data-id')||'');
   if(!text&&!labels.length) continue;
   const sig=id||text+'|'+labels.join('|');
   if(used.has(sig)) continue;used.add(sig);
   messages.push({id,text,attributes:labels,direction:node.classList.contains('message-out')?'out':node.classList.contains('message-in')?'in':'unknown'});
 }
 const editor=main?.querySelector('[contenteditable="true"][data-tab],[contenteditable="true"][role="textbox"]');
 return {
   page:'WhatsApp Web',ready:!!pane,conversation_open:!!main,
   preview_count:previews.length,previews,
   messages,composer_visible:!!editor,
   conversation_header:main?clean(main.querySelector('header')?.innerText):null,
   interface_status:!pane?'not_loaded_or_login_required':null
 };
})()
""";

    private const string ScrollScript = """
(async()=>{
 const pane=document.querySelector('#pane-side');
 if(!pane)return {complete:false,error:'chat_list_not_loaded',previews:[]};
 const all=new Map();
 const clean=s=>String(s||'').replace(/\s+/g,' ').trim();
 const findScroller=()=>{
   let nodes=[pane,...pane.querySelectorAll('*')];
   return nodes.filter(n=>n.scrollHeight>n.clientHeight+80&&n.clientHeight>80)
     .sort((a,b)=>(b.scrollHeight-b.clientHeight)-(a.scrollHeight-a.clientHeight))[0]||pane;
 };
 const scroller=findScroller(),initial=scroller.scrollTop;
 let still=0, complete=false, count=0;
 try{
   for(let i=0;i<450;i++){
     count++;
     let rows=[...pane.querySelectorAll('[role="row"],[data-testid="cell-frame-container"]')];
     rows=rows.filter(n=>n.getClientRects().length && !rows.some(p=>p!==n&&n.contains(p)&&p.matches('[role="row"],[data-testid="cell-frame-container"]')));
     let added=0;
     for(const node of rows){
       const text=clean(node.innerText);
       if(!text)continue;
       const labels=[...new Set([...node.querySelectorAll('[title],[aria-label],[data-icon]')]
         .filter(e=>e.getClientRects().length)
         .flatMap(e=>[e.title,e.getAttribute('aria-label'),e.getAttribute('data-icon')])
         .map(clean).filter(Boolean))];
       const key=text+'|'+labels.join('|');
       if(!all.has(key)){all.set(key,{text,attributes:labels});added++}
     }
     const before=scroller.scrollTop;
     const nearBottom=before+scroller.clientHeight>=scroller.scrollHeight-4;
     if(nearBottom&&added===0){if(++still>=3){complete=true;break}}
     else still=0;
     scroller.scrollTop=Math.min(scroller.scrollHeight,scroller.scrollTop+Math.max(150,scroller.clientHeight*.8));
     await new Promise(resolve=>setTimeout(resolve,75));
     if(scroller.scrollTop===before&&added===0){if(++still>=3){complete=nearBottom;break}}
   }
 }finally{scroller.scrollTop=initial}
 return {complete,preview_count:all.size,previews:[...all.values()],scroll_steps:count,opened_conversations:0};
})()
""";

    private const string FocusEditorScript = """
(()=>{
 const main=document.querySelector('#main');
 const editor=main?.querySelector('[contenteditable="true"][data-tab],[contenteditable="true"][role="textbox"]');
 if(!editor)return false;
 editor.focus();
 return document.activeElement===editor || editor.contains(document.activeElement);
})()
""";

    private const string OutgoingScript = """
(()=>{
 const main=document.querySelector('#main');
 if(!main)return {count:0,last:null};
 const nodes=[...main.querySelectorAll('.message-out')].filter(x=>x.getClientRects().length);
 return {count:nodes.length,last:nodes.length?String(nodes.at(-1).innerText||'').trim():null};
})()
""";

    private async Task<JsonNode?> EvaluateAsync(string script, CancellationToken ct)
    {
        var data=await _browser.RunJsAsync(script,ct);
        if (data?["ok"]?.GetValue<bool>() != true)
            return new JsonObject { ["error"]=data?["error"]?.GetValue<string>()??"dom_read_failed" };
        return data["value"]?.DeepClone();
    }

    public async Task<JsonNode?> ReadVisibleAsync(CancellationToken ct) =>
        await EvaluateAsync(VisibleScript,ct);

    // On the first model turn, wait for WhatsApp's asynchronously rendered sidebar.
    // A loaded page with zero rendered rows is not yet a useful initial snapshot.
    public async Task<JsonNode?> ReadReadyAsync(CancellationToken ct, int maxWaitMs = 12000)
    {
        var timer = System.Diagnostics.Stopwatch.StartNew();
        JsonNode? view;
        do
        {
            view = await ReadVisibleAsync(ct);
            if (view?["ready"]?.GetValue<bool>() == true && (view?["preview_count"]?.GetValue<int>() ?? 0) > 0)
                return view;
            var remainingMs = maxWaitMs - (int)timer.ElapsedMilliseconds;
            if (remainingMs <= 0) return view;
            await Task.Delay(Math.Min(400, remainingMs), ct);
        }
        while (true);
    }

    public async Task<JsonNode?> ReadAllPreviewsAsync(CancellationToken ct) =>
        await EvaluateAsync(ScrollScript,ct);

    // WhatsApp's direct phone route resolves asynchronously after browser navigation.
    private async Task<JsonNode?> WaitForConversationAsync(CancellationToken ct, int maxWaitMs = 12000)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        JsonNode? view;
        do
        {
            view = await ReadVisibleAsync(ct);
            if (view?["conversation_open"]?.GetValue<bool>() == true &&
                view?["composer_visible"]?.GetValue<bool>() == true) return view;
            var remaining = maxWaitMs - (int)clock.ElapsedMilliseconds;
            if (remaining <= 0) return view;
            await Task.Delay(Math.Min(400, remaining), ct);
        }
        while (true);
    }

    public async Task<JsonNode?> OpenAsync(string target, CancellationToken ct)
    {
        var url=DirectUrl(target);
        if(url is null)return new JsonObject{["ok"]=false,["error"]="unsupported_identifier_for_direct_open"};
        await _browser.NavigateAsync(url,ct);
        var visible=await WaitForConversationAsync(ct);
        var ready = visible?["conversation_open"]?.GetValue<bool>() == true &&
                    visible?["composer_visible"]?.GetValue<bool>() == true;
        return new JsonObject{["ok"]=ready,["method"]="direct_whatsapp_phone_route",
            ["error"]=ready?null:"conversation_not_ready_after_navigation",["view"]=visible?.DeepClone()};
    }

    // Only a newly visible outgoing message with the requested exact text confirms the UI send.
    // Comparison uses DOM message identity (when available), not the total count of virtualized bubbles.
    public static bool HasNewOutgoingMessage(JsonNode? before, JsonNode? after, string message)
    {
        static string Clean(string s) => string.Join(" ", s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        static List<string> Matches(JsonNode? view, string needle)
        {
            var found = new List<string>();
            if (view?["messages"] is not JsonArray messages) return found;
            foreach (var node in messages)
            {
                if (node?["direction"]?.GetValue<string>() != "out") continue;
                var text = Clean(node?["text"]?.GetValue<string>() ?? "");
                if (text.Contains(needle, StringComparison.Ordinal))
                    found.Add(node?["id"]?.GetValue<string>() ?? "");
            }
            return found;
        }
        var needle = Clean(message);
        if (needle.Length == 0) return false;
        var oldMatches = Matches(before, needle);
        var newMatches = Matches(after, needle);
        if (newMatches.Count == 0) return false;
        if (newMatches.Count > oldMatches.Count) return true;
        return newMatches.Any(id => id.Length > 0 && !oldMatches.Contains(id));
    }

    public async Task<JsonNode?> SendAsync(string target,string message,CancellationToken ct)
    {
        if (message.Length==0) return new JsonObject{["status"]="error",["error"]="empty_message"};
        var url=DirectUrl(target);
        if (url is null) return new JsonObject{["status"]="error",["error"]="unsupported_identifier_for_direct_open"};
        await _browser.NavigateAsync(url,ct);
        var before = await WaitForConversationAsync(ct);
        if (before?["conversation_open"]?.GetValue<bool>() != true ||
            before?["composer_visible"]?.GetValue<bool>() != true)
            return new JsonObject{["status"]="error",["error"]="conversation_not_ready_for_send"};
        var focus = await EvaluateAsync(FocusEditorScript,ct);
        if (focus is not JsonValue f || !f.TryGetValue<bool>(out var focused)||!focused)
            return new JsonObject{["status"]="error",["error"]="composer_not_available"};
        await _browser.InsertTextAsync(message,ct);
        var sent=await _browser.PressKeyAsync("Enter",ct);
        if(sent?["ok"]?.GetValue<bool>()!=true)
            return new JsonObject{["status"]="error",["error"]="send_key_failed"};
        var clock = System.Diagnostics.Stopwatch.StartNew();
        JsonNode? visible;
        bool matched;
        do
        {
            visible = await ReadVisibleAsync(ct);
            matched = HasNewOutgoingMessage(before, visible, message);
            if (matched || clock.ElapsedMilliseconds >= 9000) break;
            await Task.Delay(350,ct);
        }
        while (true);
        return new JsonObject
        {
            ["status"]=matched?"sent_ui":"unconfirmed",
            ["confirmation"]=matched?"new_outgoing_bubble_visible":"outgoing_bubble_not_confirmed",
            ["view"]=visible?.DeepClone()
        };
    }
}
