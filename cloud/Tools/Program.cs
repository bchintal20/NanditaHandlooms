using System.Text.Json.Nodes;
using Boutique.Core;
using Microsoft.Data.SqlClient;
using System.Data;
var connection=Environment.GetEnvironmentVariable("BoutiqueSqlConnection")??throw new InvalidOperationException("Set BoutiqueSqlConnection in the process environment.");
var store=new SqlLedgerStore(connection);
if(args.Length==1 && args[0]=="setup") { await store.SetupAsync();Console.WriteLine("Schema ready."); }
else if(args.Length==1 && args[0]=="setup-accounts") {
    var b=new SqlConnectionStringBuilder(connection){Encrypt=true,TrustServerCertificate=false,Pooling=false};
    await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();
    const string fingerprint="SELECT CONCAT(Revision,':',CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),Payload)),2)) FROM dbo.BoutiqueLedger WHERE Id=1";
    await using var check=new SqlCommand(fingerprint,c);var before=await check.ExecuteScalarAsync();
    await using var q=new SqlCommand(SqlAccountStore.Schema,c);await q.ExecuteNonQueryAsync();
    if(!Equals(before,await check.ExecuteScalarAsync()))throw new InvalidOperationException("Ledger changed during migration; inspect concurrent business writes.");
    Console.WriteLine("Account schema and restricted grants ready; financial revision/payload unchanged.");
}
else if(args.Length==2 && args[0]=="owner-invite") {
    // Save sensitive invitation to a private file; never print it into command logs.
    var email=Environment.GetEnvironmentVariable("BoutiqueOwnerEmail")??throw new InvalidOperationException("Set BoutiqueOwnerEmail.");
    var accounts=new SqlAccountStore(connection);
    var token=await accounts.RunAsync(s=>BoutiqueAccounts.Invite(s,email,"Boutique owner","owner",DateTimeOffset.UtcNow),true);
    await File.WriteAllTextAsync(args[1],token);Console.WriteLine("Owner setup token saved to private file.");
}
else if(args.Length==2 && args[0]=="owner-reset") {
    var email=Environment.GetEnvironmentVariable("BoutiqueOwnerEmail")??throw new InvalidOperationException("Set BoutiqueOwnerEmail.");
    var token=await new SqlAccountStore(connection).RunAsync(s=>BoutiqueAccounts.OwnerReset(s,email,DateTimeOffset.UtcNow),true);
    await File.WriteAllTextAsync(args[1],token);Console.WriteLine("Owner reset token saved to private file.");
}
else if(args.Length==1 && args[0]=="provision-user") {
    var password=Environment.GetEnvironmentVariable("BoutiqueAppPassword")??throw new InvalidOperationException("Set BoutiqueAppPassword in the process environment.");
    if(password.Length<24 || password.Length>128) throw new ArgumentException("Use a generated app password between 24 and 128 characters.");
    var b=new SqlConnectionStringBuilder(connection){Encrypt=true,TrustServerCertificate=false,Pooling=false};
    await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();
    var sql=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"app-user.sql"));
    await using var q=new SqlCommand(sql,c);q.Parameters.Add("@password",SqlDbType.NVarChar,128).Value=password;
    await q.ExecuteNonQueryAsync();Console.WriteLine("Limited application user grants configured.");
}
else if(args.Length==2 && args[0]=="import") {
    var input=LedgerValidator.Validate(JsonNode.Parse(await File.ReadAllTextAsync(args[1]))!.AsObject());
    await store.SaveAsync(input,import:true);
    var actual=await store.ReadAsync();
    if(!JsonNode.DeepEquals(input,actual)) throw new InvalidOperationException("Import verification failed");
    Console.WriteLine("Import verified: payload equivalence and revision preserved.");
} else if(args.Length==2 && args[0]=="verify") {
    var expected=LedgerValidator.Validate(JsonNode.Parse(await File.ReadAllTextAsync(args[1]))!.AsObject());
    var actual=await store.ReadAsync();
    if(!JsonNode.DeepEquals(expected,actual)) throw new InvalidOperationException("Cloud/source payload mismatch");
    var b=new SqlConnectionStringBuilder(connection){Encrypt=true,TrustServerCertificate=false,Pooling=false};
    await using var c=new SqlConnection(b.ConnectionString);await c.OpenAsync();
    await using(var q=new SqlCommand("SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE'), HAS_PERMS_BY_NAME('dbo.BoutiqueLedger','OBJECT','ALTER'), HAS_PERMS_BY_NAME('dbo.BoutiqueLedger','OBJECT','UPDATE'), HAS_PERMS_BY_NAME('dbo.BoutiqueHistory','OBJECT','INSERT')",c)) {
        await using var r=await q.ExecuteReaderAsync();await r.ReadAsync();
        if(r.GetInt32(0)!=0 || r.GetInt32(1)!=0 || r.GetInt32(2)!=1 || r.GetInt32(3)!=1) throw new InvalidOperationException("Application permissions mismatch");
    }
    await using(var tx=(SqlTransaction)await c.BeginTransactionAsync()) {
        await using var q=new SqlCommand("UPDATE dbo.BoutiqueLedger SET UpdatedUtc=UpdatedUtc WHERE Id=1; INSERT dbo.BoutiqueHistory(Revision,Payload) VALUES(-1,@payload);",c,tx);
        q.Parameters.Add("@payload",SqlDbType.NVarChar,-1).Value=actual.ToJsonString();await q.ExecuteNonQueryAsync();
        await tx.RollbackAsync();
    }
    if(!JsonNode.DeepEquals(actual,await store.ReadAsync())) throw new InvalidOperationException("Rollback changed ledger");
    var stale=(JsonObject)actual.DeepClone();stale["revision"]=checked((long)LedgerValidator.N(stale,"revision",0,long.MaxValue,true)+1);
    try {await store.SaveAsync(stale);throw new InvalidOperationException("Stale save accepted");} catch(LedgerConflictException) {}
    Console.WriteLine("Verified source equivalence, restricted permissions, transactional rollback and stale-revision rejection; ledger unchanged.");
} else throw new ArgumentException("Usage: setup | setup-accounts | owner-invite <private-token-file> | owner-reset <private-token-file> | provision-user | import <backup.json> | verify <backup.json>. Import only works on an empty database.");
