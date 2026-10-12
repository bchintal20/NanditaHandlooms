using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Boutique.Core;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
namespace Boutique.Api;

public sealed class AccountEndpoints {
    public const string CookieName="__Host-boutique_session";
    public static string? Cookie(HttpRequestData req){
        var header=req.Headers.TryGetValues("Cookie",out var values)?string.Join(";",values):"";
        return header.Split(';',StringSplitOptions.TrimEntries).FirstOrDefault(x=>x.StartsWith(CookieName+"=",StringComparison.Ordinal))?[(CookieName.Length+1)..];
    }
    static SqlAccountStore Store()=>new(Environment.GetEnvironmentVariable("BoutiqueSqlConnection")??throw new InvalidOperationException("Cloud storage unavailable."));
    public static bool SameOrigin(HttpRequestData req)=>req.Headers.TryGetValues("Origin",out var values)&&string.Equals(values.SingleOrDefault(),Environment.GetEnvironmentVariable("BoutiquePublicOrigin"),StringComparison.OrdinalIgnoreCase)&&!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BoutiquePublicOrigin"));
    public static async Task<AccountIdentity?> Authorize(HttpRequestData req) {
        // Missing/malformed cookies are rejected without a SQL wakeup.
        var token=Cookie(req);if(!AccountSecurity.TokenShape(token))return null;
        return await Store().RunAsync(s=>BoutiqueAccounts.Authenticate(s,token,DateTimeOffset.UtcNow));
    }
    [Function("Accounts")]
    public async Task<HttpResponseData> Accounts([HttpTrigger(AuthorizationLevel.Anonymous,"get","post",Route="account/{operation}")]HttpRequestData req,string operation){
        try{
            if(req.Method=="GET"){
                if(operation!="me"&&operation!="users")return await Reply(req,HttpStatusCode.MethodNotAllowed,new{error="Use POST"});
                var user=await Authorize(req);
                if(user is null)return await Reply(req,HttpStatusCode.Unauthorized,new{error="Sign in to your boutique."});
                if(operation=="users"){
                    if(!user.Admin)return await Reply(req,HttpStatusCode.Forbidden,new{error="Owner access required."});
                    return await Reply(req,HttpStatusCode.OK,new{users=await Store().RunAsync(BoutiqueAccounts.List)});
                }
                return await Reply(req,HttpStatusCode.OK,new{email=user.Email,name=user.Name,admin=user.Admin,bootstrap=false});
            }
            if(!SameOrigin(req))return await Reply(req,HttpStatusCode.Forbidden,new{error="Request must come from this app."});
            var type=req.Headers.TryGetValues("Content-Type",out var types)?types.FirstOrDefault():"";
            if(type?.Split(';')[0].Trim()!="application/json")return await Reply(req,HttpStatusCode.UnsupportedMediaType,new{error="Use application/json"});
            using var memory=new MemoryStream();var buffer=new byte[4096];int count;
            while((count=await req.Body.ReadAsync(buffer))>0){if(memory.Length+count>8192)return await Reply(req,HttpStatusCode.RequestEntityTooLarge,new{error="Request too large."});await memory.WriteAsync(buffer.AsMemory(0,count));}
            var input=JsonNode.Parse(memory.ToArray())!.AsObject();string Field(string key)=>input[key]?.GetValue<string>()??"";
            var now=DateTimeOffset.UtcNow;
            // Atomic persisted limits across Functions instances, not a per-process dictionary.
            if(new[]{"login","activate","recover"}.Contains(operation)){
                var result=await Store().RunAsync<object>(s=>{
                    BoutiqueAccounts.Rate(s,"public-auth",100,now);
                    BoutiqueAccounts.Rate(s,operation+":"+(operation=="activate"?AccountSecurity.Digest(Field("token")):Field("email").Trim().ToLowerInvariant()),5,now);
                    return operation switch{
                        "login"=>BoutiqueAccounts.Login(s,Field("email"),Field("password"),input["remember"]?.GetValue<bool>()==true,now),
                        "activate"=>BoutiqueAccounts.Activate(s,Field("token"),Field("password"),now),
                        _=>BoutiqueAccounts.Recover(s,Field("email"),Field("code"),Field("password"))};
                },true);
                if(result is AccountLogin login){var response=await Reply(req,HttpStatusCode.OK,new{ok=true});
                    response.Headers.Add("Set-Cookie",$"{CookieName}={login.Token}; Path=/; Secure; HttpOnly; SameSite=Strict"+(input["remember"]?.GetValue<bool>()==true?$"; Max-Age={(int)(login.Expires-now).TotalSeconds}":""));return response;}
                return await Reply(req,HttpStatusCode.OK,result);
            }
            if(operation=="logout"){
                var token=Cookie(req);if(AccountSecurity.TokenShape(token))await Store().RunAsync(s=>{BoutiqueAccounts.Logout(s,token!);return true;},true);
                var response=await Reply(req,HttpStatusCode.OK,new{ok=true});response.Headers.Add("Set-Cookie",$"{CookieName}=; Path=/; Secure; HttpOnly; SameSite=Strict; Max-Age=0");return response;
            }
            var owner=await Authorize(req);
            if(owner is null)return await Reply(req,HttpStatusCode.Unauthorized,new{error="Sign in to your boutique."});
            if(operation=="password"){
                var result=await Store().RunAsync(s=>{BoutiqueAccounts.Rate(s,"password:"+owner.Id,5,now);return BoutiqueAccounts.ChangePassword(s,owner,Field("current"),Field("password"));},true);
                return await Reply(req,HttpStatusCode.OK,result);
            }
            if(!owner.Admin)return await Reply(req,HttpStatusCode.Forbidden,new{error="Owner access required."});
            if(owner.SignedInUtc<now.AddMinutes(-15))return await Reply(req,HttpStatusCode.Forbidden,new{error="Sign out and sign in again before managing accounts."});
            if(operation=="invite"){
                const string role="member";
                var token=await Store().RunAsync(s=>BoutiqueAccounts.Invite(s,Field("email"),Field("name"),role,now),true);
                return await Reply(req,HttpStatusCode.OK,new{token});
            }
            if(operation=="reinvite")return await Reply(req,HttpStatusCode.OK,new{token=await Store().RunAsync(s=>BoutiqueAccounts.Reinvite(s,Field("id"),now),true)});
            if(operation=="reset")return await Reply(req,HttpStatusCode.OK,new{token=await Store().RunAsync(s=>BoutiqueAccounts.ResetLink(s,Field("id"),now),true)});
            if(operation=="disable"){await Store().RunAsync(s=>{BoutiqueAccounts.Disable(s,Field("id"));return true;},true);return await Reply(req,HttpStatusCode.OK,new{ok=true});}
            return await Reply(req,HttpStatusCode.NotFound,new{error="Unknown account action."});
        }catch(AccountRateException e){var response=await Reply(req,(HttpStatusCode)429,new{error=e.Message});response.Headers.Add("Retry-After","900");return response;}
        catch(AccountDeniedException e){return await Reply(req,HttpStatusCode.BadRequest,new{error=e.Message});}
        catch(Exception e) when(e is ArgumentException or JsonException or InvalidOperationException or FormatException){return await Reply(req,HttpStatusCode.BadRequest,new{error="Check the account details and try again."});}
        catch{return await Reply(req,HttpStatusCode.ServiceUnavailable,new{error="Account service is unavailable or waking up. Try again shortly."});}
    }
    static async Task<HttpResponseData> Reply(HttpRequestData req,HttpStatusCode code,object body){var response=req.CreateResponse(code);response.Headers.Add("Cache-Control","no-store");response.Headers.Add("Content-Type","application/json; charset=utf-8");await response.WriteStringAsync(JsonSerializer.Serialize(body));return response;}
}
