using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using OpenFill.Core.Browser;

namespace OpenFill.Core.Agent;

// D03: read the current Airbnb multicalendar grid in a single DOM operation.
// No clicks, no price editing and no inference that unrendered dates are complete.
public sealed class AirbnbCalendarReadService(BrowserController browser)
{
    private const string ReadScript = """
    (() => {
      const listing=__LISTING__;
      const start=__START__,end=__END__;
      if (!/(^|\.)airbnb\.com$/.test(location.hostname) ||
          !(/multicalendar|hosting\/calendar/.test(location.pathname)))
        return {status:"wrong_page",url:location.href};
      const prefix="calendar-day-"+listing+"-";
      const cells=[...document.querySelectorAll('[data-testid^="calendar-day-"]')]
        .filter(e => e.getAttribute("data-testid")?.startsWith(prefix))
        .map(el => {
          const date=el.getAttribute("data-testid").slice(prefix.length);
          if(!/^\d{4}-\d{2}-\d{2}$/.test(date)||date<start||date>end)return null;
          const rect=el.getBoundingClientRect();
          const button=el.querySelector('[role="button"],button');
          return {date, text:String(el.innerText||"").trim().slice(0,320),
            label:String(button?.getAttribute("aria-label")||"").slice(0,240),
            in_viewport:rect.bottom>0&&rect.right>0&&
              rect.left<innerWidth&&rect.top<innerHeight,
            has_action:!!button};
        }).filter(Boolean).sort((a,b)=>a.date.localeCompare(b.date));
      return {status:"ok",url:location.href,observed_at_utc:new Date().toISOString(),
        listing_id:listing,source_scope:"currently_loaded_calendar_dom",
        complete:false,cells};
    })()
    """;

    // Only a single price token occupying the entire cell is treated as a
    // parseable nightly rate. Partial/ambiguous cells remain raw observations.
    private static readonly Regex PriceToken = new(
        @"^\s*(?<whole>\d{1,3}(?:[ .\u00a0]\d{3})*|\d+)(?:[,.](?<frac>\d{1,2}))?\s*(?<unit>zł|PLN|EUR|€|USD|KSh)\s*$",
        RegexOptions.Compiled|RegexOptions.CultureInvariant|RegexOptions.IgnoreCase);

    public static bool TryParsePrice(string? text, out decimal amount, out string currency)
    {
        amount = 0m; currency = "";
        var m=PriceToken.Match(text??"");
        if (!m.Success) return false;
        var whole=m.Groups["whole"].Value.Replace(" ","").Replace(".","")
            .Replace("\u00a0","");
        var decimalText=whole+(m.Groups["frac"].Success?"."+m.Groups["frac"].Value:"");
        if (!decimal.TryParse(decimalText,NumberStyles.Number,
            CultureInfo.InvariantCulture,out amount) || amount<=0) return false;
        currency=m.Groups["unit"].Value.ToUpperInvariant() switch {
            "ZŁ" or "PLN"=>"PLN","€" or "EUR"=>"EUR","KSH"=>"KES",
            "USD"=>"USD",_=>""
        };
        return currency.Length>0;
    }

    public static JsonObject Summarize(JsonObject? raw, string listingId,
        DateOnly from, DateOnly to, decimal? percentChange=null, int priceDecimals=0)
    {
        if (raw is null || raw["status"]?.ToString() != "ok")
            return new JsonObject {
                ["status"]=raw?["status"]?.ToString() ?? "observation_missing",
                ["complete"]=false
            };
        if (raw["listing_id"]?.ToString() != listingId ||
            raw["cells"] is not JsonArray cells)
            return new JsonObject { ["status"]="listing_mismatch",["complete"]=false };
        var selected = new JsonArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int parsedCount=0;
        var units = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in cells.OfType<JsonObject>())
        {
            var day = item["date"]?.ToString() ?? "";
            if (!DateOnly.TryParseExact(day,"yyyy-MM-dd",
                    CultureInfo.InvariantCulture,DateTimeStyles.None,out var date) ||
                date<from||date>to||!seen.Add(day)) continue;
            var row=(JsonObject)item.DeepClone();
            if (TryParsePrice(row["text"]?.ToString(),out var price,out var unit))
            {
                row["observed_price"]=price; row["currency"]=unit;
                row["price_parse_status"]="single_currency_amount";
                units.Add(unit); parsedCount++;
                if (percentChange is not null)
                    row["suggested_price"]=Math.Round(
                        price*(1m+percentChange.Value/100m),priceDecimals,
                        MidpointRounding.AwayFromZero);
            }
            else row["price_parse_status"]="not_a_single_unambiguous_amount";
            selected.Add(row);
        }
        var total = to.DayNumber-from.DayNumber+1;
        return new JsonObject {
            ["status"]="ok",["source_scope"]="currently_loaded_calendar_dom",
            ["listing_id"]=listingId,["source_url"]=raw["url"]?.ToString(),
            ["observed_at_utc"]=raw["observed_at_utc"]?.ToString(),
            ["requested_dates"]=total,["observed_dates"]=selected.Count,
            ["missing_dates"]=total-selected.Count,
            ["parsed_price_dates"]=parsedCount,
            ["consistent_currency"]=units.Count==1?units.Single():null,
            ["all_requested_dates_rendered"]=selected.Count==total,
            ["all_dates_price_parsed"]=parsedCount==total && units.Count==1,
            ["percent_change_preview"]=percentChange,
            ["price_decimals"]=priceDecimals,
            ["all_prices_verified"]=false,
            ["complete"]=false,["cells"]=selected
        };
    }

    public async Task<JsonObject> ReadAsync(string listingId, string from, string to,
        CancellationToken ct, decimal? percentChange=null, int priceDecimals=0)
    {
        if (string.IsNullOrWhiteSpace(listingId) || listingId.Length>32 ||
            listingId.Any(x => x<'0'||x>'9'))
            return new JsonObject { ["status"]="invalid_listing_id" };
        if (!DateOnly.TryParseExact(from,"yyyy-MM-dd",CultureInfo.InvariantCulture,
                DateTimeStyles.None,out var first) ||
            !DateOnly.TryParseExact(to,"yyyy-MM-dd",CultureInfo.InvariantCulture,
                DateTimeStyles.None,out var last) ||
            last.DayNumber-first.DayNumber is <0 or >60)
            return new JsonObject { ["status"]="invalid_date_range" };
        if (priceDecimals is not (0 or 2) ||
            percentChange is <= -100m or > 1000m)
            return new JsonObject { ["status"]="invalid_price_preview" };
        var js=ReadScript.Replace("__LISTING__",JsonSerializer.Serialize(listingId))
            .Replace("__START__",JsonSerializer.Serialize(from))
            .Replace("__END__",JsonSerializer.Serialize(to));
        var obs=await browser.RunJsAsync(js,ct);
        if (obs?["ok"]?.GetValue<bool>() != true)
            return new JsonObject { ["status"]="dom_read_failed" };
        return Summarize(obs["value"] as JsonObject, listingId,first,last,percentChange,priceDecimals);
    }
}
