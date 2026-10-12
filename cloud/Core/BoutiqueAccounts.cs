using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Net.Mail;
namespace Boutique.Core;

public sealed class AccountDeniedException(string message="Email, password, or recovery details are incorrect.") : Exception(message);
public sealed class AccountRateException() : Exception("Too many attempts. Wait 15 minutes and try again.");
public sealed record AccountIdentity(string Id,string Email,string Name,string Role,DateTimeOffset SignedInUtc) {
    public bool Admin => Role=="owner";
}
public sealed record AccountLogin(AccountIdentity User,string Token,DateTimeOffset Expires);
public sealed record AccountActivation(string RecoveryCode);

public static class AccountSecurity {
    public const int Iterations=600000;
    public static string Token()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    public static string Digest(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    public static string Email(string value) {
        value=value.Trim().ToLowerInvariant();
        if(value.Length>254||!MailAddress.TryCreate(value,out var parsed)||parsed.Address!=value||!value.Contains('@'))throw new ArgumentException("Enter a valid email address.");
        return value;
    }
    public static void Password(string value) {
        if(value.Length<15||value.Length>128)throw new ArgumentException("Use a password or passphrase of 15–128 characters.");
    }
    public static string HashPassword(string value) {
        Password(value);var salt=RandomNumberGenerator.GetBytes(16);
        var hash=Rfc2898DeriveBytes.Pbkdf2(value,salt,Iterations,HashAlgorithmName.SHA256,32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool VerifyPassword(string value,string encoded) {
        if(value.Length>128)return false;
        try {var parts=encoded.Split('$');if(parts.Length!=4||parts[0]!="pbkdf2-sha256"||int.Parse(parts[1])!=Iterations)return false;
            return CryptographicOperations.FixedTimeEquals(Rfc2898DeriveBytes.Pbkdf2(value,Convert.FromBase64String(parts[2]),Iterations,HashAlgorithmName.SHA256,32),Convert.FromBase64String(parts[3]));
        }catch(FormatException){return false;}
    }
    public static bool TokenShape(string? value)=>value is {Length:64}&&value.All(Uri.IsHexDigit);
}

// Pure account transitions are shared by SQL transactions and isolated tests.
public static class BoutiqueAccounts {
    static readonly string Dummy=AccountSecurity.HashPassword("Dummy account password never used");
    public static JsonObject Empty()=>new(){["users"]=new JsonArray(),["sessions"]=new JsonArray(),["tokens"]=new JsonArray(),["attempts"]=new JsonArray()};
    static JsonArray Rows(JsonObject s,string key)=>s[key]!.AsArray();
    static string Text(JsonNode row,string key)=>row[key]?.GetValue<string>()??"";
    static DateTimeOffset Time(JsonNode row,string key)=>DateTimeOffset.Parse(Text(row,key),System.Globalization.CultureInfo.InvariantCulture);
    static AccountIdentity Identity(JsonNode user,DateTimeOffset signed)=>new(Text(user,"id"),Text(user,"email"),Text(user,"name"),Text(user,"role"),signed);
    static JsonNode? User(JsonObject s,string email)=>Rows(s,"users").FirstOrDefault(u=>Text(u!,"email")==email);
    static void Remove(JsonArray rows,Func<JsonNode,bool> condition){for(int i=rows.Count-1;i>=0;i--)if(condition(rows[i]!))rows.RemoveAt(i);}
    public static bool HasOwner(JsonObject s)=>Rows(s,"users").Any(u=>Text(u!,"role")=="owner"&&u!["enabled"]!.GetValue<bool>()&&Text(u,"passwordHash")!="");
    public static AccountIdentity? Authenticate(JsonObject s,string? token,DateTimeOffset now) {
        if(!AccountSecurity.TokenShape(token))return null;
        var session=Rows(s,"sessions").FirstOrDefault(x=>Text(x!,"hash")==AccountSecurity.Digest(token!)&&Time(x!,"expires")>now);
        if(session is null)return null;
        var u=Rows(s,"users").FirstOrDefault(u=>Text(u!,"id")==Text(session,"user")&&u!["enabled"]!.GetValue<bool>()&&Text(u,"passwordHash")!="");
        return u is null?null:Identity(u,Time(session,"created"));
    }
    public static void Prune(JsonObject s,DateTimeOffset now) {
        foreach(var key in new[]{"sessions","tokens"})Remove(Rows(s,key),x=>Time(x,"expires")<=now);
        Remove(Rows(s,"attempts"),x=>Time(x,"start")<=now.AddMinutes(-15));
    }
    public static void Rate(JsonObject s,string key,int limit,DateTimeOffset now) {
        Prune(s,now);key=AccountSecurity.Digest(key);
        var attempt=Rows(s,"attempts").FirstOrDefault(x=>Text(x!,"key")==key);
        if(attempt is null){attempt=new JsonObject{["key"]=key,["count"]=0,["start"]=now.ToString("O")};Rows(s,"attempts").Add(attempt);}
        if(attempt["count"]!.GetValue<int>()>=limit)throw new AccountRateException();
        attempt["count"]=attempt["count"]!.GetValue<int>()+1;
    }
    public static string Invite(JsonObject s,string email,string name,string role,DateTimeOffset now) {
        email=AccountSecurity.Email(email);name=name.Trim();if(name.Length<1||name.Length>100)throw new ArgumentException("Enter a name of 1–100 characters.");
        if(role!="owner"&&role!="member")throw new ArgumentException("Invalid account role.");
        if(Rows(s,"users").Count>=20&&User(s,email) is null)throw new ArgumentException("This boutique supports up to 20 accounts.");
        var u=User(s,email);
        if(u is not null&&Text(u,"passwordHash")!="")throw new ArgumentException("That account already exists. Use a reset link instead.");
        if(role=="owner"&&Rows(s,"users").Any(x=>Text(x!,"role")=="owner"&&Text(x!,"email")!=email))throw new ArgumentException("The owner account is already assigned.");
        if(u is null){u=new JsonObject{["id"]=Guid.NewGuid().ToString("N"),["email"]=email,["name"]=name,["role"]=role,["enabled"]=true,["passwordHash"]="",["recoveryHash"]=""};Rows(s,"users").Add(u);}
        return Issue(s,u,"invite",now);
    }
    static string Issue(JsonObject s,JsonNode u,string kind,DateTimeOffset now) {
        Remove(Rows(s,"tokens"),x=>Text(x,"user")==Text(u,"id"));var token=AccountSecurity.Token();
        Rows(s,"tokens").Add(new JsonObject{["hash"]=AccountSecurity.Digest(token),["user"]=Text(u,"id"),["kind"]=kind,["expires"]=now.AddHours(24).ToString("O")});return token;
    }
    public static string OwnerReset(JsonObject s,string email,DateTimeOffset now) {
        var u=User(s,AccountSecurity.Email(email));if(u is null||Text(u,"role")!="owner"||!u["enabled"]!.GetValue<bool>()||Text(u,"passwordHash")=="")throw new ArgumentException("Activated owner not found.");
        return Issue(s,u,"reset",now);
    }
    public static string ResetLink(JsonObject s,string id,DateTimeOffset now) {
        var u=Rows(s,"users").FirstOrDefault(x=>Text(x!,"id")==id&&x!["enabled"]!.GetValue<bool>()&&Text(x,"passwordHash")!="")??throw new ArgumentException("Account unavailable.");
        return Issue(s,u,"reset",now);
    }
    public static AccountActivation Activate(JsonObject s,string token,string password,DateTimeOffset now) {
        AccountSecurity.Password(password);
        if(!AccountSecurity.TokenShape(token))throw new AccountDeniedException("This setup or reset link is invalid or expired.");
        var entry=Rows(s,"tokens").FirstOrDefault(x=>Text(x!,"hash")==AccountSecurity.Digest(token)&&Time(x!,"expires")>now);
        var u=entry is null?null:Rows(s,"users").FirstOrDefault(x=>Text(x!,"id")==Text(entry,"user")&&x!["enabled"]!.GetValue<bool>());
        if(u is null)throw new AccountDeniedException("This setup or reset link is invalid or expired.");
        return SetPassword(s,u,password);
    }
    static AccountActivation SetPassword(JsonObject s,JsonNode u,string password) {
        var recovery=AccountSecurity.Token();u["passwordHash"]=AccountSecurity.HashPassword(password);u["recoveryHash"]=AccountSecurity.Digest(recovery);
        Remove(Rows(s,"sessions"),x=>Text(x,"user")==Text(u,"id"));Remove(Rows(s,"tokens"),x=>Text(x,"user")==Text(u,"id"));
        return new(recovery);
    }
    public static AccountActivation Recover(JsonObject s,string email,string code,string password) {
        AccountSecurity.Password(password);var u=User(s,AccountSecurity.Email(email));
        if(u is null||!u["enabled"]!.GetValue<bool>()||!AccountSecurity.TokenShape(code)||Text(u,"recoveryHash")!=AccountSecurity.Digest(code))throw new AccountDeniedException();
        return SetPassword(s,u,password);
    }
    public static AccountLogin Login(JsonObject s,string email,string password,bool remember,DateTimeOffset now) {
        email=AccountSecurity.Email(email);var u=User(s,email);bool matches=AccountSecurity.VerifyPassword(password,u is null?Dummy:Text(u,"passwordHash") is {Length:>0} hash?hash:Dummy);
        if(!matches||u is null||!u["enabled"]!.GetValue<bool>()||Text(u,"passwordHash")=="")throw new AccountDeniedException();
        Remove(Rows(s,"attempts"),x=>Text(x,"key")==AccountSecurity.Digest("login:"+email));
        Remove(Rows(s,"sessions"),x=>Text(x,"user")==Text(u,"id")&&Time(x,"expires")<=now);
        var existing=Rows(s,"sessions").Where(x=>Text(x!,"user")==Text(u,"id")).ToArray();
        foreach(var old in existing.Take(Math.Max(0,existing.Length-9)))Rows(s,"sessions").Remove(old);
        var token=AccountSecurity.Token();var expiry=remember?now.AddDays(30):now.AddHours(12);
        Rows(s,"sessions").Add(new JsonObject{["hash"]=AccountSecurity.Digest(token),["user"]=Text(u,"id"),["created"]=now.ToString("O"),["expires"]=expiry.ToString("O")});
        return new(Identity(u,now),token,expiry);
    }
    public static void Logout(JsonObject s,string token)=>Remove(Rows(s,"sessions"),x=>Text(x,"hash")==AccountSecurity.Digest(token));
    public static AccountActivation ChangePassword(JsonObject s,AccountIdentity identity,string current,string password) {
        var u=Rows(s,"users").First(x=>Text(x!,"id")==identity.Id);
        if(!AccountSecurity.VerifyPassword(current,Text(u!,"passwordHash")))throw new AccountDeniedException();
        return SetPassword(s,u!,password);
    }
    public static string Reinvite(JsonObject s,string id,DateTimeOffset now) {
        var u=Rows(s,"users").FirstOrDefault(x=>Text(x!,"id")==id)??throw new ArgumentException("Account unavailable.");
        if(Text(u,"role")=="owner"||u["enabled"]!.GetValue<bool>())throw new ArgumentException("Use invitation or reset for that account.");
        u["enabled"]=true;u["passwordHash"]="";u["recoveryHash"]="";return Issue(s,u,"invite",now);
    }
    public static void Disable(JsonObject s,string id) {
        var u=Rows(s,"users").FirstOrDefault(x=>Text(x!,"id")==id)??throw new ArgumentException("Account unavailable.");
        if(Text(u,"role")=="owner")throw new ArgumentException("The owner account cannot be disabled.");
        u["enabled"]=false;Remove(Rows(s,"sessions"),x=>Text(x,"user")==id);Remove(Rows(s,"tokens"),x=>Text(x,"user")==id);u["recoveryHash"]="";
    }
    public static object[] List(JsonObject s)=>Rows(s,"users").Select(u=>(object)new{id=Text(u!,"id"),email=Text(u!,"email"),name=Text(u!,"name"),role=Text(u!,"role"),enabled=u!["enabled"]!.GetValue<bool>(),pending=Text(u,"passwordHash")==""}).ToArray();
}
