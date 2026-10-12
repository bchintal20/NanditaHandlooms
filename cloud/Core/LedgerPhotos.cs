using System.Text.Json.Nodes;
namespace Boutique.Core;

// The stored ledger and backups retain JPEG data. Browser state holds private URLs.
public static class LedgerPhotos
{
    public const string Stored = "__stored_photo__";
    public static string Reference(string product, string? variant, long revision)
        => "/api/photo?product=" + Uri.EscapeDataString(product)
           + (variant is null ? "" : "&variant=" + Uri.EscapeDataString(variant))
           + "&revision=" + revision;

    public static JsonObject BrowserState(JsonObject ledger)
    {
        var result = (JsonObject)ledger.DeepClone();
        var revision = checked((long)LedgerValidator.N(result,"revision",0,long.MaxValue,true));
        foreach (var node in result["products"]!.AsArray()) {
            var p=node!.AsObject();var pid=LedgerValidator.Text(p,"id");
            Replace(p,Reference(pid,null,revision));
            if(p["variants"] is JsonArray variants) foreach(var row in variants) {
                var v=row!.AsObject();Replace(v,Reference(pid,LedgerValidator.Text(v,"id"),revision));
            }
        }
        result["lazyPhotos"]=true;
        return result;
    }
    static void Replace(JsonObject record,string url) {
        var photo=record["photo"]?.GetValue<string>();
        if(!string.IsNullOrEmpty(photo))record["photo"]=url;
    }

    public static JsonObject ResolveForSave(JsonObject incoming,JsonObject current)
    {
        var result=(JsonObject)incoming.DeepClone();result.Remove("lazyPhotos");
        var revision=checked((long)LedgerValidator.N(current,"revision",0,long.MaxValue,true));
        var existing=current["products"]!.AsArray().ToDictionary(p=>LedgerValidator.Text(p!.AsObject(),"id"));
        foreach(var row in result["products"]!.AsArray()) {
            var p=row!.AsObject();var pid=LedgerValidator.Text(p,"id");existing.TryGetValue(pid,out var previous);
            Resolve(p,previous?.AsObject(),Reference(pid,null,revision));
            if(p["variants"] is JsonArray variants) {
                var oldVariants=(previous?["variants"] as JsonArray ?? new JsonArray()).ToDictionary(v=>LedgerValidator.Text(v!.AsObject(),"id"));
                foreach(var entry in variants) {
                    var v=entry!.AsObject();var vid=LedgerValidator.Text(v,"id");oldVariants.TryGetValue(vid,out var old);
                    Resolve(v,old?.AsObject(),Reference(pid,vid,revision));
                }
            }
        }
        return result;
    }
    static void Resolve(JsonObject incoming,JsonObject? original,string expected) {
        var photo=incoming["photo"]?.GetValue<string>();
        if(string.IsNullOrEmpty(photo)||photo.StartsWith("data:image/jpeg;base64,"))return;
        if(photo!=expected || original is null || string.IsNullOrEmpty(original["photo"]?.GetValue<string>()))
            throw new ArgumentException("Invalid stored photo reference");
        incoming["photo"]=original["photo"]!.DeepClone();
    }
}
