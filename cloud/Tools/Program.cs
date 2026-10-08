using System.Text.Json.Nodes;
using Boutique.Core;
using Microsoft.Data.SqlClient;
using System.Data;
var connection=Environment.GetEnvironmentVariable("BoutiqueSqlConnection")??throw new InvalidOperationException("Set BoutiqueSqlConnection in the process environment.");
var store=new SqlLedgerStore(connection);
if(args.Length==1 && args[0]=="setup") { await store.SetupAsync();Console.WriteLine("Schema ready."); }
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
} else throw new ArgumentException("Usage: setup | provision-user | import <backup.json>. Import only works on an empty database.");
