using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Boutique.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
namespace Boutique.Api;
public sealed class Endpoints
{
    const int MaxBytes=15000000;
    [Function("Health")]
    public Task<HttpResponseData> Health([HttpTrigger(AuthorizationLevel.Anonymous,"get",Route="health")]HttpRequestData req)
        =>Reply(req,HttpStatusCode.OK,new {ok=true});
    [Function("Ledger")]
    public async Task<HttpResponseData> Ledger([HttpTrigger(AuthorizationLevel.Anonymous,"get","post",Route="{operation:regex(^(state|restore)$)}")]HttpRequestData req,string operation)
    {
        var email=Environment.GetEnvironmentVariable("BoutiqueOwnerEmail");
        if(string.IsNullOrWhiteSpace(email)) return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Cloud access has not been configured"});
        var principal=req.Headers.TryGetValues("x-ms-client-principal",out var values)?values.FirstOrDefault():null;
        if(!Identity.IsOwner(principal,email)) return await Reply(req,HttpStatusCode.Forbidden,new{error="This account does not have access to the boutique"});
        var connection=Environment.GetEnvironmentVariable("BoutiqueSqlConnection");
        if(string.IsNullOrWhiteSpace(connection)) return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Cloud storage has not been configured"});
        if(operation=="restore" && req.Method!="POST") return await Reply(req,HttpStatusCode.MethodNotAllowed,new{error="Use POST"});
        try {
            var store=new SqlLedgerStore(connection);
            if(req.Method=="GET") return await Reply(req,HttpStatusCode.OK,await store.ReadAsync());
            var origin=req.Headers.TryGetValues("Origin",out var origins)?origins.FirstOrDefault():null;
            var expected=Environment.GetEnvironmentVariable("BoutiquePublicOrigin");
            if(string.IsNullOrWhiteSpace(expected) || !string.Equals(origin,expected,StringComparison.OrdinalIgnoreCase))
                return await Reply(req,HttpStatusCode.Forbidden,new{error="Request must come from this app"});
            var type=req.Headers.TryGetValues("Content-Type",out var types)?types.FirstOrDefault():"";
            if(!string.Equals(type?.Split(';')[0].Trim(),"application/json",StringComparison.OrdinalIgnoreCase))
                return await Reply(req,HttpStatusCode.UnsupportedMediaType,new{error="Use application/json"});
            using var buffer=new MemoryStream();var chunk=new byte[8192];int count;
            while((count=await req.Body.ReadAsync(chunk))>0) {
                if(buffer.Length+count>MaxBytes) return await Reply(req,HttpStatusCode.RequestEntityTooLarge,new{error="Request too large"});
                await buffer.WriteAsync(chunk.AsMemory(0,count));
            }
            var input=JsonNode.Parse(buffer.ToArray())?.AsObject()??throw new ArgumentException("Invalid ledger");
            return await Reply(req,HttpStatusCode.OK,await store.SaveAsync(input));
        } catch(LedgerConflictException e) { return await Reply(req,HttpStatusCode.Conflict,new{error=e.Message}); }
        catch(LedgerNotInitializedException e) { return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error=e.Message}); }
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException or FormatException or OverflowException) {
            return await Reply(req,HttpStatusCode.BadRequest,new{error="Invalid ledger. Check required fields and quantities."});
        }
        catch { return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Cloud database is unavailable or waking up. Wait briefly and refresh; your last saved data is retained."}); }
    }
    static async Task<HttpResponseData> Reply(HttpRequestData req,HttpStatusCode status,object value) {
        var res=req.CreateResponse(status);res.Headers.Add("Cache-Control","no-store");
        res.Headers.Add("Content-Type","application/json; charset=utf-8");
        await res.WriteStringAsync(JsonSerializer.Serialize(value));return res;
    }
}
