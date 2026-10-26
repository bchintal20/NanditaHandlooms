using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Boutique.Core;
using Boutique.Api;
using Microsoft.Data.SqlClient;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Collections.ObjectModel;
int checks=0;
void Check(bool ok,string message) {if(!ok)throw new Exception(message);checks++;}
void Reject(JsonObject state,string message) {try{LedgerValidator.Validate(state);throw new Exception(message);}catch(ArgumentException){checks++;}}
var source=JsonNode.Parse(await File.ReadAllTextAsync(args[0]))!.AsObject();
var validated=LedgerValidator.Validate(source);
var guardOld=(JsonObject)source.DeepClone();var guardP=(JsonObject)source["products"]![0]!.DeepClone();guardP["id"]="guarded";guardP["reserved"]=1;guardP["openingSold"]=0;guardOld["products"]!.AsArray().Add(guardP);
var guardNew=(JsonObject)guardOld.DeepClone();guardNew["products"]!.AsArray().RemoveAt(guardNew["products"]!.AsArray().Count-1);
try{LedgerValidator.ValidateChanges(guardOld,guardNew);throw new Exception("Reserved deletion accepted");}catch(ArgumentException){checks++;}
guardP["reserved"]=0;LedgerValidator.ValidateChanges(guardOld,guardNew);checks++;
guardOld["sales"]!.AsArray().Add(new JsonObject{["id"]="guard-sale",["product"]="guarded",["variant"]="",["qty"]=1,["price"]=50});
try{LedgerValidator.ValidateChanges(guardOld,guardNew);throw new Exception("Sold batch deletion accepted");}catch(ArgumentException){checks++;}
guardOld["returns"]!.AsArray().Add(new JsonObject{["id"]="guard-return",["sale"]="guard-sale"});guardNew=(JsonObject)guardOld.DeepClone();guardNew["sales"]!.AsArray().Last()!["price"]=55;LedgerValidator.ValidateChanges(guardOld,guardNew);checks++;
guardNew["sales"]!.AsArray().Last()!["qty"]=2;try{LedgerValidator.ValidateChanges(guardOld,guardNew);throw new Exception("Refunded quantity edit accepted");}catch(ArgumentException){checks++;}

Check(validated["sales"]!.AsArray().Count==source["sales"]!.AsArray().Count,"Sales lost in validation");
Check(JsonNode.DeepEquals(source["pending"],validated["pending"]),"Pending review changed");
var copy=(JsonObject)validated.DeepClone();copy["sales"]!.AsArray().Add(copy["sales"]![0]!.DeepClone());Reject(copy,"Duplicate sale accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["qty"]=100000;Reject(copy,"Overselling accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["qty"]=1.5m;Reject(copy,"Fractional sale accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["shippingCharge"]=-1;Reject(copy,"Negative shipping accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["taxable"]=false;copy["sales"]![0]!["tax"]=1;Reject(copy,"Nontaxable sale with tax accepted");
copy=(JsonObject)validated.DeepClone();
var colorProduct=(JsonObject)copy["products"]![0]!.DeepClone();
colorProduct["id"]="colors";colorProduct["qty"]=10;colorProduct["reserved"]=0;colorProduct["openingSold"]=0;colorProduct["shipment"]="";
colorProduct["variants"]=JsonNode.Parse("[{\"id\":\"red\",\"name\":\"Red\",\"qty\":3},{\"id\":\"blue\",\"name\":\"Blue\",\"qty\":5}]");
copy["products"]!.AsArray().Add(colorProduct);
var colorSale=new JsonObject{["id"]="color-sale",["product"]="colors",["variant"]="red",["qty"]=3,["date"]="2026-10-09",["payment"]="Cash",["price"]=150,["tax"]=0,["expense"]=0,["taxable"]=true,["reason"]=""};
copy["sales"]!.AsArray().Add(colorSale);LedgerValidator.Validate(copy);checks++;
colorSale["qty"]=4;Reject(copy,"Color overselling accepted");colorSale["qty"]=3;
colorSale["variant"]="missing";Reject(copy,"Missing variant accepted");
colorSale["variant"]="";Reject(copy,"Unassigned overselling accepted");colorSale["qty"]=2;LedgerValidator.Validate(copy);checks++;
colorProduct["variants"]![0]!["qty"]=4;Reject(copy,"Overallocation accepted");colorProduct["variants"]![0]!["qty"]=3;
colorSale["variant"]="red";colorSale["qty"]=3;
var colorReturn=new JsonObject{["id"]="color-return",["sale"]="color-sale",["date"]="2026-10-10",["restock"]=true};copy["returns"]!.AsArray().Add(colorReturn);
var resale=(JsonObject)colorSale.DeepClone();resale["id"]="color-resale";copy["sales"]!.AsArray().Add(resale);LedgerValidator.Validate(copy);checks++;
colorReturn["restock"]=false;Reject(copy,"Non-restocked color reused");colorReturn["restock"]=true;
colorProduct["variants"]![0]!["photo"]="data:text/html,test";Reject(copy,"Unsafe variant media accepted");colorProduct["variants"]![0]!.AsObject().Remove("photo");
colorProduct["variants"]![0]!["instagram"]="https://example.com";Reject(copy,"Foreign color link accepted");colorProduct["variants"]![0]!.AsObject().Remove("instagram");
colorProduct["variants"]![0]!["qty"]=1.5m;Reject(copy,"Fractional variant accepted");colorProduct["variants"]![0]!["qty"]=3;
colorProduct["variants"]![0]!["name"]="Blue";Reject(copy,"Duplicate color accepted");colorProduct["variants"]![0]!["name"]="Red";
colorProduct["variants"]=new JsonArray();Reject(copy,"Referenced variant deleted");
var photoFixture=(JsonObject)validated.DeepClone();
var photoBatch=(JsonObject)photoFixture["products"]![0]!.DeepClone();
photoBatch["id"]="photo / product";photoBatch["openingSold"]=0;photoBatch["qty"]=2;photoBatch["reserved"]=0;photoBatch["shipment"]="";
photoBatch["photo"]="data:image/jpeg;base64,"+new string('A',700000);
photoBatch["variants"]=new JsonArray(new JsonObject{["id"]="blue + variant",["name"]="Blue",["qty"]=1,["photo"]="data:image/jpeg;base64,"+new string('B',400000)});
photoFixture["products"]!.AsArray().Add(photoBatch);LedgerValidator.Validate(photoFixture);
var untouched=(JsonObject)photoFixture.DeepClone();var lightweight=LedgerPhotos.BrowserState(photoFixture);
Check(JsonNode.DeepEquals(photoFixture,untouched),"Photo projection mutated source");
Check(lightweight.ToJsonString().Length<photoFixture.ToJsonString().Length/5,"Images still in startup state");
Check(lightweight["products"]!.AsArray().Last()!["photo"]!.GetValue<string>().StartsWith("/api/photo?product=photo%20%2F%20product"),"Photo reference did not encode identifier");
var resolved=LedgerPhotos.ResolveForSave(lightweight,photoFixture);
Check(JsonNode.DeepEquals(resolved,photoFixture),"Photo roundtrip lost original data");
var edited=(JsonObject)lightweight.DeepClone();edited["settings"]!["name"]="Edited boutique";
resolved=LedgerPhotos.ResolveForSave(edited,photoFixture);LedgerValidator.Validate(resolved);
Check(JsonNode.DeepEquals(resolved["products"],photoFixture["products"]),"Non-photo edit discarded photos");
edited=(JsonObject)lightweight.DeepClone();edited["products"]!.AsArray().Last()!["photo"]="";
resolved=LedgerPhotos.ResolveForSave(edited,photoFixture);Check(resolved["products"]!.AsArray().Last()!["photo"]!.GetValue<string>()=="","Explicit removal ignored");
edited=(JsonObject)lightweight.DeepClone();edited["products"]!.AsArray().Last()!["variants"]![0]!["photo"]="data:image/jpeg;base64,YWJj";
resolved=LedgerPhotos.ResolveForSave(edited,photoFixture);Check(resolved["products"]!.AsArray().Last()!["variants"]![0]!["photo"]!.GetValue<string>()=="data:image/jpeg;base64,YWJj","Replacement ignored");
edited=(JsonObject)lightweight.DeepClone();edited["products"]!.AsArray().Last()!["photo"]="/api/photo?product=other";
try{LedgerPhotos.ResolveForSave(edited,photoFixture);throw new Exception("Foreign photo reference accepted");}catch(ArgumentException){checks++;}
edited=(JsonObject)lightweight.DeepClone();edited["products"]!.AsArray().Last()!["photo"]="https://example.com/image";
try{LedgerPhotos.ResolveForSave(edited,photoFixture);throw new Exception("External photo reference accepted");}catch(ArgumentException){checks++;}
var newer=(JsonObject)photoFixture.DeepClone();newer["revision"]=LedgerValidator.N(newer,"revision")+1;
try{LedgerPhotos.ResolveForSave(lightweight,newer);throw new Exception("Outdated photo reference accepted");}catch(ArgumentException){checks++;}
string Principal(string email,string[] roles)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(new JsonObject{["identityProvider"]="aad",["userId"]="test-user",["userDetails"]=email,["userRoles"]=new JsonArray(roles.Select(r=>(JsonNode?)JsonValue.Create(r)).ToArray())}.ToJsonString()));
Check(Identity.IsOwner(Principal("owner@example.com",["boutique-owner"]),"OWNER@example.com"),"Owner denied");
Check(!Identity.IsOwner(Principal("other@example.com",["boutique-owner"]),"owner@example.com"),"Wrong owner accepted");
Check(!Identity.IsOwner(Principal("owner@example.com",["authenticated"]),"owner@example.com"),"Uninvited account accepted");
Check(!Identity.IsOwner("bad base64", "owner@example.com"),"Malformed identity accepted");
Check(!Identity.IsOwner(null,"owner@example.com"),"Anonymous account accepted");
Environment.SetEnvironmentVariable("BoutiqueOwnerEmail","owner@example.com");
Environment.SetEnvironmentVariable("BoutiqueSqlConnection","not-a-connection"); // Failed authentication must precede SQL parsing/access.
Environment.SetEnvironmentVariable("BoutiquePublicOrigin","https://boutique.example.com");
var endpoints=new Endpoints();
var health=await endpoints.Health(new Request("GET"));Check(health.StatusCode==HttpStatusCode.OK,"Health queried SQL");
var denied=await endpoints.Ledger(new Request("GET"),"state");Check(denied.StatusCode==HttpStatusCode.Unauthorized,"Anonymous endpoint allowed");
var foreign=new Request("POST");foreign.Headers.Add("x-ms-client-principal",Principal("owner@example.com",["boutique-owner"]));foreign.Headers.Add("Origin","https://evil.example.com");foreign.Headers.Add("Content-Type","application/json");
denied=await endpoints.Ledger(foreign,"state");Check(denied.StatusCode==HttpStatusCode.Unauthorized,"Cross-origin write accepted");
var photoDenied=await endpoints.Photo(new Request("GET"));Check(photoDenied.StatusCode==HttpStatusCode.Unauthorized,"Anonymous photo access allowed");
var accountEndpoints=new AccountEndpoints();
foreach(var action in new[]{"login","activate","recover","invite","reset","disable","password","logout"}) {
    var response=await accountEndpoints.Accounts(new Request("POST"),action);
    Check(response.StatusCode==HttpStatusCode.Forbidden,"Cross-origin account mutation accepted: "+action);
}
var cookieRequest=new Request("GET");cookieRequest.Headers.Add("Cookie","__Host-boutique_session=invalid");
Check(await AccountEndpoints.Authorize(cookieRequest) is null,"Malformed cookie accepted");
var platformOnly=new Request("GET");platformOnly.Headers.Add("x-ms-client-principal",foreign.Headers.GetValues("x-ms-client-principal").First());
Check((await endpoints.Ledger(platformOnly,"state")).StatusCode==HttpStatusCode.Unauthorized,"Microsoft principal bypassed boutique login");
var remoteTest=new SqlLedgerStore("Server=cloud.database.windows.net;Database=x;Integrated Security=true",true);
try{await remoteTest.ReadAsync();throw new Exception("Remote test-certificate override allowed");}catch(InvalidOperationException){checks++;}
if(args.Contains("--sql")) {
    var db="NanditaCloudValidation_"+Guid.NewGuid().ToString("N");
    var admin="Server=localhost;Database=master;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Pooling=false";
    await using var c=new SqlConnection(admin);await c.OpenAsync();
    await using(var q=new SqlCommand("CREATE DATABASE ["+db+"]",c))await q.ExecuteNonQueryAsync();
    try {
        var store=new SqlLedgerStore(admin.Replace("Database=master","Database="+db),true);
        await store.SetupAsync();await store.SaveAsync(validated,true);
        Check(JsonNode.DeepEquals(validated,await store.ReadAsync()),"Initial import differs");
        try{await store.SaveAsync(validated,true);throw new Exception("Duplicate import accepted");}catch(LedgerConflictException){checks++;}
        var initial=await store.ReadAsync();
        var a=(JsonObject)initial.DeepClone();var b=(JsonObject)initial.DeepClone();a["settings"]!["name"]="First";b["settings"]!["name"]="Second";
        async Task<bool> Save(JsonObject s){try{await store.SaveAsync(s);return true;}catch(LedgerConflictException){return false;}}
        var outcomes=await Task.WhenAll(Save(a),Save(b));
        Check(outcomes.Count(x=>x)==1,"Concurrent saves did not produce exactly one winner");
        var saved=await store.ReadAsync();
        Check(LedgerValidator.N(saved,"revision")==LedgerValidator.N(initial,"revision")+1,"Revision increment incorrect");
        var restore=(JsonObject)initial.DeepClone();restore["revision"]=saved["revision"]!.DeepClone();
        var restored=await store.SaveAsync(restore);
        Check(JsonNode.DeepEquals(initial["sales"],restored["sales"]),"Restore lost sales");
        var invalid=(JsonObject)restored.DeepClone();invalid["sales"]![0]!["qty"]=100000;
        try{await store.SaveAsync(invalid);throw new Exception("Invalid SQL save accepted");}catch(ArgumentException){checks++;}
        Check(JsonNode.DeepEquals(restored,await store.ReadAsync()),"Failed save changed database");
        await using var q=new SqlCommand("SELECT COUNT(*) FROM ["+db+"].dbo.BoutiqueHistory",c);
        Check(Convert.ToInt32(await q.ExecuteScalarAsync())==2,"Recovery history missing");
    } finally {
        if(!db.StartsWith("NanditaCloudValidation_")||!System.Text.RegularExpressions.Regex.IsMatch(db,"^[A-Za-z0-9_]+$"))throw new Exception("Unsafe test DB name");
        await using var drop=new SqlCommand("ALTER DATABASE ["+db+"] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ["+db+"]",c);await drop.ExecuteNonQueryAsync();
    }
}
Console.WriteLine($"PASS: {checks} checks"+(args.Contains("--sql")?" including SQL import, concurrent writes, restore, rollback and history.":"."));
sealed class Request(string method):HttpRequestData(new Context())
{
    public override Stream Body {get;}=new MemoryStream();
    public override HttpHeadersCollection Headers {get;}=new();
    public override IReadOnlyCollection<IHttpCookie> Cookies=>Array.Empty<IHttpCookie>();
    public override Uri Url=>new("https://boutique.example.com/api/state");
    public override IEnumerable<ClaimsIdentity> Identities=>Array.Empty<ClaimsIdentity>();
    public override string Method=>method;
    public override HttpResponseData CreateResponse()=>new Response();
}
sealed class Response():HttpResponseData(new Context())
{
    public override HttpStatusCode StatusCode {get;set;}
    public override HttpHeadersCollection Headers {get;set;}=new();
    public override Stream Body {get;set;}=new MemoryStream();
    public override HttpCookies Cookies=>null!;
}
sealed class Context:FunctionContext
{
    public override string InvocationId=>"test";
    public override string FunctionId=>"test";
    public override TraceContext TraceContext=>null!;
    public override BindingContext BindingContext=>null!;
    public override RetryContext RetryContext=>null!;
    public override IServiceProvider InstanceServices {get;set;}=new ServiceCollection().BuildServiceProvider();
    public override FunctionDefinition FunctionDefinition=>null!;
    public override IDictionary<object,object> Items {get;set;}=new Dictionary<object,object>();
    public override IInvocationFeatures Features=>null!;
}
