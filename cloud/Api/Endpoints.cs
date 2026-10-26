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
        try { if(await AccountEndpoints.Authorize(req) is null)return await Reply(req,HttpStatusCode.Unauthorized,new{error="Sign in to your boutique."}); }
        catch { return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Account service is unavailable or waking up. Try again shortly."}); }
        var connection=Environment.GetEnvironmentVariable("BoutiqueSqlConnection");
        if(string.IsNullOrWhiteSpace(connection)) return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Cloud storage has not been configured"});
        if(operation=="restore" && req.Method!="POST") return await Reply(req,HttpStatusCode.MethodNotAllowed,new{error="Use POST"});
        try {
            var store=new SqlLedgerStore(connection);
            if(req.Method=="GET") {
                var lazy=System.Web.HttpUtility.ParseQueryString(req.Url.Query)["photos"]=="lazy";
                if(lazy)return await Reply(req,HttpStatusCode.OK,await store.ReadBrowserStateAsync());
                return await Backup(req,await store.ReadAsync());
            }
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
            if(operation=="restore"&&input["lazyPhotos"]?.GetValue<bool>()==true)throw new ArgumentException("Restore needs a full backup with photos");
            var saved=await store.SaveAsync(input,restore:operation=="restore");
            if(System.Web.HttpUtility.ParseQueryString(req.Url.Query)["photos"]=="lazy")return await Reply(req,HttpStatusCode.OK,LedgerPhotos.BrowserState(saved));
            return await Backup(req,saved);
        } catch(LedgerConflictException e) { return await Reply(req,HttpStatusCode.Conflict,new{error=e.Message}); }
        catch(LedgerTooLargeException e) { return await Reply(req,HttpStatusCode.RequestEntityTooLarge,new{error=e.Message}); }
        catch(LedgerNotInitializedException e) { return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error=e.Message}); }
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException or FormatException or OverflowException) {
            return await Reply(req,HttpStatusCode.BadRequest,new{error="Invalid ledger. Check required fields and quantities."});
        }
        catch { return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Cloud database is unavailable or waking up. Wait briefly and refresh; your last saved data is retained."}); }
    }
    [Function("Photo")]
    public async Task<HttpResponseData> Photo([HttpTrigger(AuthorizationLevel.Anonymous,"get",Route="photo")]HttpRequestData req) {
        try { if(await AccountEndpoints.Authorize(req) is null)return await Reply(req,HttpStatusCode.Unauthorized,new{error="Sign in to your boutique."}); }
        catch { return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Account service is unavailable or waking up. Try again shortly."}); }
        try {
            var args=System.Web.HttpUtility.ParseQueryString(req.Url.Query);var pid=args["product"];var variant=args["variant"];
            if(string.IsNullOrWhiteSpace(pid))return await Reply(req,HttpStatusCode.BadRequest,new{error="Missing product"});
            var photo=await new SqlLedgerStore(Environment.GetEnvironmentVariable("BoutiqueSqlConnection")!).ReadPhotoAsync(pid,variant);
            const string prefix="data:image/jpeg;base64,";
            if(string.IsNullOrEmpty(photo)||!photo.StartsWith(prefix))return await Reply(req,HttpStatusCode.NotFound,new{error="Photo not found"});
            var response=req.CreateResponse(HttpStatusCode.OK);response.Headers.Add("Content-Type","image/jpeg");response.Headers.Add("Cache-Control","no-store");
            await response.Body.WriteAsync(Convert.FromBase64String(photo[prefix.Length..]));return response;
        }catch{return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Photo unavailable. Try refreshing."});}
    }
    static async Task<HttpResponseData> Reply(HttpRequestData req,HttpStatusCode status,object value) {
        var res=req.CreateResponse(status);res.Headers.Add("Cache-Control","no-store");
        res.Headers.Add("Content-Type","application/json; charset=utf-8");
        await res.WriteStringAsync(JsonSerializer.Serialize(value));return res;
    }
    static async Task<HttpResponseData> Backup(HttpRequestData req,JsonObject ledger) {
        var res=req.CreateResponse(HttpStatusCode.OK);res.Headers.Add("Cache-Control","no-store");
        res.Headers.Add("Content-Type","application/json; charset=utf-8");res.Headers.Add("Content-Encoding","gzip");
        using var compressed=new MemoryStream();
        using(var zip=new System.IO.Compression.GZipStream(compressed,System.IO.Compression.CompressionLevel.Fastest,true))
            await zip.WriteAsync(System.Text.Encoding.UTF8.GetBytes(ledger.ToJsonString()));
        compressed.Position=0;await compressed.CopyToAsync(res.Body);return res;
    }
}
