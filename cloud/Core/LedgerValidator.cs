using System.Globalization;
using System.Text.Json.Nodes;
namespace Boutique.Core;
public static class LedgerValidator
{
    public static JsonObject Validate(JsonObject input)
    {
        var s = (JsonObject)input.DeepClone();
        if (N(s, "version") != 1) throw new ArgumentException("Unsupported backup version");
        N(s, "revision", 0, long.MaxValue, true);
        var products = Rows(s, "products"); var shipments = Rows(s, "shipments");
        var sales = Rows(s, "sales"); var returns = Rows(s, "returns"); var pending = Rows(s, "pending");
        var pm = products.ToDictionary(x => Text(x,"id")); var sm = shipments.ToDictionary(x => Text(x,"id"));
        var settings = s["settings"]!.AsObject();
        N(settings,"margin",0,99.9m); N(settings,"tax",0,30); N(settings,"exchange",0.000001m);
        if (!new[]{"margin","markup"}.Contains(Optional(settings,"pricing","margin"))) Fail("Invalid pricing mode");
        foreach(var sh in shipments) {
            if (!new[]{"quantity","value","weight","manual"}.Contains(Text(sh,"method"))) Fail("Invalid allocation method");
            foreach(var k in new[]{"domestic","international","duty","brokerage","other"}) N(sh,k);
        }
        foreach(var p in products) {
            Text(p,"name");Text(p,"sku");Date(p,"date");
            p["openingSold"] ??= 0;
            N(p,"qty",1,10000000000m,true); N(p,"reserved",0,10000000000m,true); N(p,"openingSold",0,10000000000m,true);
            foreach(var k in new[]{"purchase","packaging","weight","share"}) N(p,k);
            N(p,"exchange",0.000001m);
            if (!new[]{"INR","USD"}.Contains(Text(p,"currency"))) Fail("Invalid currency");
            var shipment=Optional(p,"shipment","");
            if(shipment.Length>0 && !sm.ContainsKey(shipment)) Fail("Missing shipment");
            var link=Optional(p,"instagram","");
            if(link.Length>0 && (!Uri.TryCreate(link,UriKind.Absolute,out var u) || u.Scheme!="https" || !new[]{"instagram.com","www.instagram.com"}.Contains(u.Host))) Fail("Invalid Instagram URL");
            var photo=Optional(p,"photo","");
            if(photo.Length>0 && (!photo.StartsWith("data:image/jpeg;base64,") || photo.Length>1000000)) Fail("Invalid photo");
        }
        var sold=pm.ToDictionary(x=>x.Key,x=>N(x.Value,"openingSold"));
        foreach(var sale in sales) {
            Date(sale,"date");
            if(!new[]{"Zelle","Venmo","Cash","Other"}.Contains(Text(sale,"payment"))) Fail("Invalid payment method");
            foreach(var k in new[]{"price","tax","expense"}) N(sale,k);
            sale["shippingCharge"] ??= 0;
            if(!sale.ContainsKey("shippingCost")) sale["shippingCost"]=0;
            bool taxable=sale["taxable"]!.GetValue<bool>();
            sale["shippingTaxable"] ??= taxable ? N(sale,"shippingCharge") : 0;
            var charge=N(sale,"shippingCharge");
            if(sale["shippingCost"] is not null) N(sale,"shippingCost");
            var shipTax=N(sale,"shippingTaxable",0,charge);
            if(!taxable && shipTax!=0) Fail("Nontaxable sale has taxable shipping");
            if(taxable && shipTax<charge && Optional(sale,"shippingReason","").Trim().Length==0) Fail("Shipping exemption needs documentation");
            if(!taxable && (N(sale,"tax")!=0 || Optional(sale,"reason","").Trim().Length==0)) Fail("Nontaxable sales require a reason and zero tax");
            var pid=Text(sale,"product");
            if(!pm.ContainsKey(pid)) Fail("Missing sale product");
            sold[pid]+=N(sale,"qty",1,10000000000m,true);
        }
        var saleMap=sales.ToDictionary(x=>Text(x,"id"));var refunded=new HashSet<string>();
        foreach(var r in returns) {
            Date(r,"date");var id=Text(r,"sale");
            if(!saleMap.ContainsKey(id) || !refunded.Add(id)) Fail("Each sale may have one full refund");
            var sale=saleMap[id];
            if(Date(r,"date")<Date(sale,"date")) Fail("Refund cannot precede sale");
            if(r["restock"]!.GetValue<bool>()) sold[Text(sale,"product")]-=N(sale,"qty");
        }
        foreach(var p in products) if(sold[Text(p,"id")]+N(p,"reserved")>N(p,"qty")) Fail("Sold plus reserved exceeds received quantity");
        var pendingCounts=pm.ToDictionary(x=>x.Key,_=>0m);
        foreach(var p in pending) {
            var pid=Text(p,"product");if(!pm.ContainsKey(pid)) Fail("Missing historical sale product");
            pendingCounts[pid]+=N(p,"qty");N(p,"total");
        }
        foreach(var p in products) if(pendingCounts[Text(p,"id")]!=N(p,"openingSold")) Fail("Historical review quantities do not match opening sold quantities");
        foreach(var sh in shipments) {
            var group=products.Where(p=>Optional(p,"shipment","")==Text(sh,"id")).ToArray();
            var method=Text(sh,"method");
            if(group.Length>0 && (method=="weight"||method=="manual") && group.Any(p=>N(p,method=="weight"?"weight":"share")<=0)) Fail("Shipment requires positive allocation weights");
            if(group.Length>0 && method=="value" && group.Sum(p=>N(p,"purchase")*N(p,"qty"))==0) Fail("Value allocation needs purchase costs");
        }
        return s;
    }
    public static decimal N(JsonObject o,string key,decimal min=0,decimal max=10000000000m,bool whole=false)
    {
        decimal v;
        if(o[key] is not JsonValue number || !decimal.TryParse(number.ToJsonString(),NumberStyles.Float,CultureInfo.InvariantCulture,out v)) throw new ArgumentException(key+" is invalid");
        if(v<min || v>max || (whole && decimal.Truncate(v)!=v)) Fail(key+" is invalid");
        return v;
    }
    public static string Text(JsonObject o,string key) {
        var v=o[key]?.GetValue<string>();if(string.IsNullOrWhiteSpace(v)) Fail(key+" is required");return v!;
    }
    static string Optional(JsonObject o,string key,string fallback)=>o[key]?.GetValue<string>()??fallback;
    static DateOnly Date(JsonObject o,string key) {
        if(!DateOnly.TryParseExact(Text(o,key),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var d)) Fail("Invalid date"); return d;
    }
    static List<JsonObject> Rows(JsonObject s,string key) {
        if(key=="pending" && !s.ContainsKey(key)) s[key]=new JsonArray();
        if(s[key] is not JsonArray a || a.Count>100000) throw new ArgumentException("Invalid "+key);
        var rows=a.Select(x=>x?.AsObject()??throw new ArgumentException("Invalid "+key)).ToList();
        var ids=key=="pending" ? Array.Empty<string>() : rows.Select(x=>Text(x,"id")).ToArray();
        // Historical-review IDs were not required by the original app; preserve that contract.
        if(key!="pending" && ids.Distinct().Count()!=ids.Length) Fail("Duplicate record IDs");
        return rows;
    }
    static void Fail(string message)=>throw new ArgumentException(message);
}
