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
Check(validated["sales"]!.AsArray().Count==source["sales"]!.AsArray().Count,"Sales lost in validation");
Check(JsonNode.DeepEquals(source["pending"],validated["pending"]),"Pending review changed");
var copy=(JsonObject)validated.DeepClone();copy["sales"]!.AsArray().Add(copy["sales"]![0]!.DeepClone());Reject(copy,"Duplicate sale accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["qty"]=100000;Reject(copy,"Overselling accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["qty"]=1.5m;Reject(copy,"Fractional sale accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["shippingCharge"]=-1;Reject(copy,"Negative shipping accepted");
copy=(JsonObject)validated.DeepClone();copy["sales"]![0]!["taxable"]=false;copy["sales"]![0]!["tax"]=1;Reject(copy,"Nontaxable sale with tax accepted");
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
var denied=await endpoints.Ledger(new Request("GET"),"state");Check(denied.StatusCode==HttpStatusCode.Forbidden,"Anonymous endpoint allowed");
var foreign=new Request("POST");foreign.Headers.Add("x-ms-client-principal",Principal("owner@example.com",["boutique-owner"]));foreign.Headers.Add("Origin","https://evil.example.com");foreign.Headers.Add("Content-Type","application/json");
denied=await endpoints.Ledger(foreign,"state");Check(denied.StatusCode==HttpStatusCode.Forbidden,"Cross-origin write accepted");
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
