using Boutique.Core;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
void Denied(Action action){try{action();throw new Exception("Denied action accepted");}catch(Exception e) when(e is AccountDeniedException or AccountRateException or ArgumentException){checks++;}}
var now=DateTimeOffset.Parse("2026-10-12T12:00:00Z");const string password="A boutique passphrase for testing";
Check(AccountSecurity.Email(" Owner@Example.com ")=="owner@example.com","Email normalization");
Denied(()=>AccountSecurity.Email("bad email"));Denied(()=>AccountSecurity.Password("short"));Denied(()=>AccountSecurity.Password(new string('a',129)));
var hash=AccountSecurity.HashPassword(password);Check(AccountSecurity.VerifyPassword(password,hash),"Correct password denied");Check(!AccountSecurity.VerifyPassword("wrong",hash),"Wrong password accepted");Check(hash!=AccountSecurity.HashPassword(password),"Password salts reused");Check(!AccountSecurity.VerifyPassword(password,"broken"),"Malformed hash accepted");
var s=BoutiqueAccounts.Empty();var token=BoutiqueAccounts.Invite(s,"owner@example.com","Owner","owner",now);
Check(!BoutiqueAccounts.HasOwner(s),"Pending owner active");Check(!s.ToJsonString().Contains(token),"Raw invitation stored");
Denied(()=>BoutiqueAccounts.Activate(s,token,password,now.AddHours(25)));Denied(()=>BoutiqueAccounts.Activate(s,AccountSecurity.Token(),password,now));
var activation=BoutiqueAccounts.Activate(s,token,password,now);Check(BoutiqueAccounts.HasOwner(s),"Active owner missing");Check(!s.ToJsonString().Contains(activation.RecoveryCode),"Raw recovery stored");Denied(()=>BoutiqueAccounts.Activate(s,token,password,now));
Denied(()=>BoutiqueAccounts.Invite(s,"other@example.com","Other","owner",now));Denied(()=>BoutiqueAccounts.Invite(s,"owner@example.com","Owner","owner",now));
Denied(()=>BoutiqueAccounts.Login(s,"owner@example.com","wrong",false,now));Denied(()=>BoutiqueAccounts.Login(s,"unknown@example.com",password,false,now));
var login=BoutiqueAccounts.Login(s,"OWNER@example.com",password,true,now);Check(login.Expires==now.AddDays(30),"Remember lifetime");Check(!s.ToJsonString().Contains(login.Token),"Raw session stored");Check(BoutiqueAccounts.Authenticate(s,login.Token,now)?.Admin==true,"Owner role missing");Check(BoutiqueAccounts.Authenticate(s,login.Token,now.AddDays(31)) is null,"Expired session accepted");Check(BoutiqueAccounts.Authenticate(s,"malformed",now) is null,"Malformed session accepted");
var regular=BoutiqueAccounts.Login(s,"owner@example.com",password,false,now);Check(regular.Expires==now.AddHours(12),"Regular lifetime");BoutiqueAccounts.Logout(s,regular.Token);Check(BoutiqueAccounts.Authenticate(s,regular.Token,now) is null,"Logout failed");
Denied(()=>BoutiqueAccounts.Disable(s,login.User.Id));Denied(()=>BoutiqueAccounts.Recover(s,"owner@example.com","wrong",password));
var recovery=BoutiqueAccounts.Recover(s,"owner@example.com",activation.RecoveryCode,password+" changed");Check(BoutiqueAccounts.Authenticate(s,login.Token,now) is null,"Recovery kept sessions");Denied(()=>BoutiqueAccounts.Recover(s,"owner@example.com",activation.RecoveryCode,password));
var changed=BoutiqueAccounts.Login(s,"owner@example.com",password+" changed",true,now);Denied(()=>BoutiqueAccounts.ChangePassword(s,changed.User,"wrong",password));BoutiqueAccounts.ChangePassword(s,changed.User,password+" changed",password);Check(BoutiqueAccounts.Authenticate(s,changed.Token,now) is null,"Change kept sessions");
var memberToken=BoutiqueAccounts.Invite(s,"member@example.com","Member","member",now);BoutiqueAccounts.Activate(s,memberToken,password,now);var member=BoutiqueAccounts.Login(s,"member@example.com",password,false,now);Check(!member.User.Admin,"Member became owner");
var reset=BoutiqueAccounts.ResetLink(s,member.User.Id,now);var reset2=BoutiqueAccounts.ResetLink(s,member.User.Id,now);Denied(()=>BoutiqueAccounts.Activate(s,reset,password,now));BoutiqueAccounts.Activate(s,reset2,password,now);Check(BoutiqueAccounts.Authenticate(s,member.Token,now) is null,"Reset kept sessions");
member=BoutiqueAccounts.Login(s,"member@example.com",password,false,now);BoutiqueAccounts.Disable(s,member.User.Id);Check(BoutiqueAccounts.Authenticate(s,member.Token,now) is null,"Disabled session valid");Denied(()=>BoutiqueAccounts.Login(s,"member@example.com",password,false,now));
var reinvite=BoutiqueAccounts.Reinvite(s,member.User.Id,now);Denied(()=>BoutiqueAccounts.Login(s,"member@example.com",password,false,now));BoutiqueAccounts.Activate(s,reinvite,password,now);Check(!BoutiqueAccounts.Login(s,"member@example.com",password,false,now).User.Admin,"Reinvite role changed");
Check(!System.Text.Json.JsonSerializer.Serialize(BoutiqueAccounts.List(s)).Contains("passwordHash"),"User list leaked hashes");
for(int i=0;i<5;i++)BoutiqueAccounts.Rate(s,"testing",5,now);Denied(()=>BoutiqueAccounts.Rate(s,"testing",5,now));BoutiqueAccounts.Rate(s,"testing",5,now.AddMinutes(16));checks++;
for(int i=0;i<15;i++)BoutiqueAccounts.Login(s,"owner@example.com",password,false,now);Check(s["sessions"]!.AsArray().Count(x=>x!["user"]!.GetValue<string>()==login.User.Id)==10,"Session cap failed");

if(args.Contains("--sql")){
 var database="NanditaAccountsTest_"+Guid.NewGuid().ToString("N");const string master="Server=localhost;Database=master;Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Pooling=false";
 await using var c=new SqlConnection(master);await c.OpenAsync();await using(var create=new SqlCommand($"CREATE DATABASE [{database}]",c)){await create.ExecuteNonQueryAsync();}
 try{
 var connection=master.Replace("Database=master","Database="+database);await using(var schemaConnection=new SqlConnection(connection)){await schemaConnection.OpenAsync();await using var schema=new SqlCommand("CREATE USER boutique_app WITHOUT LOGIN;"+SqlAccountStore.Schema,schemaConnection);await schema.ExecuteNonQueryAsync();}
 var store=new SqlAccountStore(connection,true);var invitation=await store.RunAsync(s=>BoutiqueAccounts.Invite(s,"owner@example.com","Owner","owner",DateTimeOffset.UtcNow),true);
 async Task<bool> Activate(){try{await store.RunAsync(s=>BoutiqueAccounts.Activate(s,invitation,password,DateTimeOffset.UtcNow),true);return true;}catch(AccountDeniedException){return false;}}
 var results=await Task.WhenAll(Activate(),Activate());Check(results.Count(x=>x)==1,"Concurrent invitation consumed twice");
 try{await store.RunAsync(s=>{BoutiqueAccounts.Rate(s,"login:owner@example.com",5,DateTimeOffset.UtcNow);return BoutiqueAccounts.Login(s,"owner@example.com","wrong",false,DateTimeOffset.UtcNow);},true);throw new Exception("Wrong password accepted in SQL");}catch(AccountDeniedException){checks++;}
 var count=await store.RunAsync(s=>s["attempts"]!.AsArray().Count);Check(count==1,"Failed login rate not committed");
 var sqlLogin=await store.RunAsync(s=>BoutiqueAccounts.Login(s,"owner@example.com",password,true,DateTimeOffset.UtcNow),true);Check(await store.RunAsync(s=>BoutiqueAccounts.Authenticate(s,sqlLogin.Token,DateTimeOffset.UtcNow)) is not null,"SQL session failed");
 await store.RunAsync(s=>{BoutiqueAccounts.Logout(s,sqlLogin.Token);return true;},true);Check(await store.RunAsync(s=>BoutiqueAccounts.Authenticate(s,sqlLogin.Token,DateTimeOffset.UtcNow)) is null,"SQL logout failed");
 }finally{if(!System.Text.RegularExpressions.Regex.IsMatch(database,"^NanditaAccountsTest_[a-f0-9]{32}$"))throw new Exception("Unsafe test DB name");await using var drop=new SqlCommand($"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]",c);await drop.ExecuteNonQueryAsync();}
}
Console.WriteLine($"PASS: {checks} account checks"+(args.Contains("--sql")?" including isolated SQL concurrency and rate persistence.":"."));
