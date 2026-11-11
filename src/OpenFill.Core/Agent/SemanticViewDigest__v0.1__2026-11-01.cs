using System.Text.Json.Nodes;

namespace OpenFill.Core.Agent;

/// <summary>
/// Universal current-page projection for Luna. No service-specific selectors.
/// Record-oriented lists, logs and grids, with only visible rows and action labels.
/// </summary>
public static class SemanticViewDigest
{
    public const string Script = """
        (() => {
          const regionQuery = '[role="list"],[role="log"],[role="grid"],[role="table"],table,ul,ol,aside,main';
          const rowQuery = 'li,[role="listitem"],[role="row"],tr,[role="article"],article,[data-message-id]';
          const regions = Array.from(document.querySelectorAll(regionQuery)).slice(0,120);
          const visible = e => { const r=e.getBoundingClientRect();return r.width>0&&r.height>0; };
          const squeeze = (t,n) => String(t||'').replace(/\s+/g,' ').trim().slice(0,n);
          const kind = e => e.getAttribute('role') || ({TABLE:'table',UL:'list',OL:'list',ASIDE:'aside',MAIN:'main'}[e.tagName]||'region');
          const locator = e => {
            if(e.id){const sel='#'+CSS.escape(e.id);if(document.querySelectorAll(sel).length===1)return sel;}
            for(const key of ['data-thread-id','data-profile-id','data-message-id','data-testid','data-id']){
              const val=e.getAttribute(key);if(!val)continue;
              const sel='['+key+'='+JSON.stringify(val)+']';
              if(document.querySelectorAll(sel).length===1)return sel;
            }
            return null;
          };
          const ranked = regions.map(region => {
            const rows=Array.from(region.querySelectorAll(rowQuery)).filter(e =>
              visible(e) && e.closest(regionQuery)===region).slice(0,90);
            const score = (kind(region)==='log'?70:kind(region)==='table'?60:kind(region)==='grid'?55:
              kind(region)==='list'?50:kind(region)==='aside'?20:10) + Math.min(rows.length,25);
            return {region,rows,score};
          }).filter(x=>x.rows.length>=2).sort((a,b)=>b.score-a.score);
          const selected=[];
          for (const x of ranked) {
            if(selected.length>=4)break;
            if(selected.some(y=>x.region.contains(y.region)||y.region.contains(x.region)))continue;
            selected.push(x);
          }
          let remaining=52;
          const sections=selected.map(x=>{
            const role=kind(x.region), label=squeeze(x.region.getAttribute('aria-label')||
              x.region.getAttribute('data-testid')||x.region.querySelector('h1,h2,h3,[role=heading]')?.innerText,80);
            const records=x.rows.slice(0,Math.min(remaining,26)).map(e=>{
              const link=e.matches('a[href]')?e:e.querySelector('a[href]');
              const action=Array.from(e.querySelectorAll('button,[role=button]')).slice(0,2).map(b=>({
                label:squeeze(b.getAttribute('aria-label')||b.innerText||b.getAttribute('title'),50),
                selector:locator(b)}));
              return {text:squeeze(e.innerText||e.textContent,240),selector:locator(e),
                href:link?.getAttribute('href')?.slice(0,250)||null,
                actions:action.filter(a=>a.label)};
            });
            remaining-=records.length;
            return {kind:role,label,visible_rows:x.rows.length,returned:records.length,
              scrollable:x.region.scrollHeight>x.region.clientHeight+30,
              scope:'visible_or_rendered_only',records};
          });
          const controlCandidates=Array.from(document.querySelectorAll('input:not([type=hidden]),textarea,select,button,[role=button]'))
            .filter(visible).slice(0,100).map(e=>({
              label:squeeze(e.getAttribute('aria-label')||e.getAttribute('placeholder')||e.innerText||e.getAttribute('title'),72),
              type:e.tagName.toLowerCase(),selector:locator(e),disabled:!!e.disabled
            }));
          const controls=controlCandidates.filter(x=>x.selector && (x.label||x.type!=='button')).slice(0,26);
          const recordCount=sections.reduce((s,x)=>s+x.records.length,0);
          const recordLocated=sections.reduce((s,x)=>s+x.records.filter(r=>r.selector).length,0);
          return {status:'ok',url:location.href,title:squeeze(document.title,100),
            observed_at:new Date().toISOString(),scope:'currently_rendered_dom',
            full_dom_capture:false,complete:false,section_count:sections.length,
            returned_records:recordCount,
            locator_coverage:{records_considered:recordCount,records_with_selector:recordLocated,
              controls_considered:controlCandidates.length,
              controls_with_selector:controlCandidates.filter(x=>x.selector).length,
              controls_returned:controls.length},
            controls,sections};
        })()
        """;

    public static JsonObject Summarize(JsonObject? response)
    {
        if (response?["ok"]?.GetValue<bool>() != true ||
            response["value"] is not JsonObject payload ||
            payload["status"]?.ToString() != "ok" ||
            payload["sections"] is not JsonArray sections)
            return new JsonObject { ["status"] = "error", ["reason_code"] = "digest_unavailable" };
        // Refuse an unexpectedly large browser response rather than flood the model.
        if (sections.Count > 4 || payload.ToJsonString().Length > 28000)
            return new JsonObject { ["status"] = "error", ["reason_code"] = "digest_limit_exceeded" };
        return payload.DeepClone().AsObject();
    }

    // Only bounded counts are logged. Missing coverage is NOT treated as zero coverage.
    public static JsonObject LocatorCoverage(JsonObject digest)
    {
        if (digest["status"]?.ToString() != "ok" ||
            digest["locator_coverage"] is not JsonObject c)
            return new JsonObject { ["status"] = "unavailable" };

        static int? Read(JsonObject c, string key)
        {
            try { return c[key]?.GetValue<int>(); }
            catch { return null; }
        }
        var records = Read(c, "records_considered");
        var recordLocated = Read(c, "records_with_selector");
        var controls = Read(c, "controls_considered");
        var controlLocated = Read(c, "controls_with_selector");
        var returned = Read(c, "controls_returned");
        if (records is null || recordLocated is null || controls is null ||
            controlLocated is null || returned is null ||
            records < 0 || recordLocated < 0 || recordLocated > records ||
            controls < 0 || controlLocated < 0 || controlLocated > controls ||
            returned < 0 || returned > controlLocated)
            return new JsonObject { ["status"] = "invalid" };

        return new JsonObject
        {
            ["status"] = "measured",
            ["records_considered"] = records,
            ["records_with_selector"] = recordLocated,
            ["record_coverage_pct"] = records == 0 ? null : Math.Round(100.0 * recordLocated.Value / records.Value, 1),
            ["controls_considered"] = controls,
            ["controls_with_selector"] = controlLocated,
            ["control_coverage_pct"] = controls == 0 ? null : Math.Round(100.0 * controlLocated.Value / controls.Value, 1),
            ["controls_returned"] = returned
        };
    }
}
