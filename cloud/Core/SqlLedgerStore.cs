using System.Data;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
namespace Boutique.Core;
public sealed class LedgerConflictException() : Exception("Another device changed the ledger. Refresh and try again.");
public sealed class LedgerNotInitializedException() : Exception("Cloud ledger has not been imported yet.");
public sealed class LedgerTooLargeException() : Exception("The ledger and photos exceed the 15 MB backup limit. Remove or reduce photos before saving.");
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
    public async Task<JsonObject> ReadBrowserStateAsync() {
        // Remove large image strings in SQL, before they cross the database connection.
        const string query = """
            DECLARE @payload nvarchar(max) = (SELECT Payload FROM dbo.BoutiqueLedger WHERE Id=1);
            SELECT JSON_MODIFY(@payload,'$.products',JSON_QUERY('[]'));
            SELECT CAST(p.[key] AS int),
                JSON_MODIFY(JSON_MODIFY(p.[value],'$.photo',
                    CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(p.[value]) WHERE [key]='photo' AND LEN([value])>0) THEN '__stored_photo__'
                         WHEN EXISTS(SELECT 1 FROM OPENJSON(p.[value]) WHERE [key]='photo') THEN '' ELSE NULL END),'$.variants',NULL),
                CASE WHEN JSON_QUERY(p.[value],'$.variants') IS NOT NULL THEN 1 ELSE 0 END
            FROM OPENJSON(@payload,'$.products') p ORDER BY CAST(p.[key] AS int);
            SELECT CAST(p.[key] AS int),CAST(v.[key] AS int),
                JSON_MODIFY(v.[value],'$.photo',
                    CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(v.[value]) WHERE [key]='photo' AND LEN([value])>0) THEN '__stored_photo__'
                         WHEN EXISTS(SELECT 1 FROM OPENJSON(v.[value]) WHERE [key]='photo') THEN '' ELSE NULL END)
            FROM OPENJSON(@payload,'$.products') p CROSS APPLY OPENJSON(p.[value],'$.variants') v
            ORDER BY CAST(p.[key] AS int),CAST(v.[key] AS int);
            """;
        await using var c=Connection();await c.OpenAsync();
        await using var q=new SqlCommand(query,c){CommandTimeout=20};
        await using var reader=await q.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
        if(!await reader.ReadAsync()||reader.IsDBNull(0))throw new LedgerNotInitializedException();
        var result=JsonNode.Parse(reader.GetString(0))!.AsObject();var products=result["products"]!.AsArray();
        await reader.NextResultAsync();
        while(await reader.ReadAsync()) {
            reader.GetInt32(0);var p=JsonNode.Parse(reader.GetString(1))!.AsObject();
            if(reader.GetInt32(2)==1)p["variants"]=new JsonArray();products.Add(p);
        }
        await reader.NextResultAsync();
        while(await reader.ReadAsync()) {
            var index=reader.GetInt32(0);reader.GetInt32(1);
            products[index]!["variants"]!.AsArray().Add(JsonNode.Parse(reader.GetString(2)));
        }
        return LedgerPhotos.BrowserState(result);
    }
    public async Task<string?> ReadPhotoAsync(string product,string? variant) {
        const string productQuery="""
            SELECT p.photo FROM dbo.BoutiqueLedger l CROSS APPLY OPENJSON(l.Payload,'$.products')
            WITH (id nvarchar(max) '$.id',photo nvarchar(max) '$.photo') p WHERE l.Id=1 AND p.id=@product
            """;
        const string variantQuery="""
            SELECT v.photo FROM dbo.BoutiqueLedger l CROSS APPLY OPENJSON(l.Payload,'$.products')
            WITH (id nvarchar(max) '$.id',variants nvarchar(max) '$.variants' AS JSON) p
            CROSS APPLY OPENJSON(p.variants) WITH (id nvarchar(max) '$.id',photo nvarchar(max) '$.photo') v
            WHERE l.Id=1 AND p.id=@product AND v.id=@variant
            """;
        await using var c=Connection();await c.OpenAsync();
        await using var q=new SqlCommand(variant is null?productQuery:variantQuery,c){CommandTimeout=20};
        q.Parameters.Add("@product",SqlDbType.NVarChar,-1).Value=product;
        if(variant is not null)q.Parameters.Add("@variant",SqlDbType.NVarChar,-1).Value=variant;
        return await q.ExecuteScalarAsync() as string;
    }
    public async Task<JsonObject> SaveAsync(JsonObject input,bool import=false,bool restore=false) {
        var s=import?LedgerValidator.Validate(input):(JsonObject)input.DeepClone();
        var revision=checked((long)LedgerValidator.N(input,"revision",0,long.MaxValue,true));
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
            s=LedgerValidator.Validate(LedgerPhotos.ResolveForSave(s,JsonNode.Parse(currentJson!)!.AsObject()));
            if(!restore)LedgerValidator.ValidateChanges(JsonNode.Parse(currentJson!)!.AsObject(),s);
            if(System.Text.Encoding.UTF8.GetByteCount(s.ToJsonString())>15000000)throw new LedgerTooLargeException();
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
