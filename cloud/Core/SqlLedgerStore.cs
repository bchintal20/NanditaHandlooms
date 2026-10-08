using System.Data;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
namespace Boutique.Core;
public sealed class LedgerConflictException() : Exception("Another device changed the ledger. Refresh and try again.");
public sealed class LedgerNotInitializedException() : Exception("Cloud ledger has not been imported yet.");
public sealed class SqlLedgerStore(string connectionString, bool trustLocalTestCertificate = false)
{
    public const string Schema = """
        IF OBJECT_ID('dbo.BoutiqueLedger','U') IS NULL
        CREATE TABLE dbo.BoutiqueLedger (
          Id int NOT NULL PRIMARY KEY CHECK(Id=1),
          Revision bigint NOT NULL,
          Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1),
          UpdatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
        );
        IF OBJECT_ID('dbo.BoutiqueHistory','U') IS NULL
        CREATE TABLE dbo.BoutiqueHistory (
          Revision bigint NOT NULL PRIMARY KEY,
          Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1),
          SavedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
        );
        """;
    SqlConnection Connection() {
        var b=new SqlConnectionStringBuilder(connectionString) { Encrypt=true, TrustServerCertificate=false, Pooling=false };
        if(trustLocalTestCertificate) {
            if(!new[]{"localhost",".","(local)",Environment.MachineName}.Contains(b.DataSource,StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Test certificates are permitted only on this computer.");
            b.TrustServerCertificate=true;
        }
        return new SqlConnection(b.ConnectionString);
    }
    public async Task SetupAsync() {
        await using var c=Connection();await c.OpenAsync();
        await using var q=new SqlCommand(Schema,c);await q.ExecuteNonQueryAsync();
    }
    public async Task<JsonObject> ReadAsync() {
        await using var c=Connection();await c.OpenAsync();
        await using var q=new SqlCommand("SELECT Payload FROM dbo.BoutiqueLedger WHERE Id=1",c);
        var json=await q.ExecuteScalarAsync() as string??throw new LedgerNotInitializedException();
        return JsonNode.Parse(json)!.AsObject();
    }
    public async Task<JsonObject> SaveAsync(JsonObject input,bool import=false) {
        var s=LedgerValidator.Validate(input);
        var revision=checked((long)LedgerValidator.N(s,"revision",0,long.MaxValue,true));
        await using var c=Connection();await c.OpenAsync();
        await using var tx=(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable);
        await using var read=new SqlCommand("SELECT Revision,Payload FROM dbo.BoutiqueLedger WITH(UPDLOCK,HOLDLOCK) WHERE Id=1",c,tx);
        long? currentRevision=null;string? currentJson=null;
        await using(var r=await read.ExecuteReaderAsync()) if(await r.ReadAsync()) { currentRevision=r.GetInt64(0);currentJson=r.GetString(1); }
        if(import) {
            if(currentRevision is not null) throw new LedgerConflictException();
            await using var insert=new SqlCommand("INSERT dbo.BoutiqueLedger(Id,Revision,Payload) VALUES(1,@revision,@payload)",c,tx);
            insert.Parameters.Add("@revision",SqlDbType.BigInt).Value=revision;
            insert.Parameters.Add("@payload",SqlDbType.NVarChar,-1).Value=s.ToJsonString();
            await insert.ExecuteNonQueryAsync();
        } else {
            if(currentRevision is null) throw new LedgerNotInitializedException();
            if(currentRevision!=revision) throw new LedgerConflictException();
            await using var history=new SqlCommand("INSERT dbo.BoutiqueHistory(Revision,Payload) VALUES(@revision,@payload)",c,tx);
            history.Parameters.Add("@revision",SqlDbType.BigInt).Value=revision;
            history.Parameters.Add("@payload",SqlDbType.NVarChar,-1).Value=currentJson!;
            await history.ExecuteNonQueryAsync();
            s["revision"]=checked(revision+1);
            await using var update=new SqlCommand("UPDATE dbo.BoutiqueLedger SET Revision=@revision,Payload=@payload,UpdatedUtc=SYSUTCDATETIME() WHERE Id=1",c,tx);
            update.Parameters.Add("@revision",SqlDbType.BigInt).Value=revision+1;
            update.Parameters.Add("@payload",SqlDbType.NVarChar,-1).Value=s.ToJsonString();
            await update.ExecuteNonQueryAsync();
            // Keep a bounded history; downloadable backups remain available independently.
            await using var trim=new SqlCommand("DELETE dbo.BoutiqueHistory WHERE Revision < @cutoff",c,tx);
            trim.Parameters.Add("@cutoff",SqlDbType.BigInt).Value=Math.Max(0,revision-49);
            await trim.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        return s;
    }
}
