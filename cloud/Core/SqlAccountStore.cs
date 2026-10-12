using System.Data;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
namespace Boutique.Core;

public sealed class SqlAccountStore(string connectionString,bool trustLocalTestCertificate=false) {
    public const string Schema="""
        IF OBJECT_ID('dbo.BoutiqueAccounts','U') IS NULL
        CREATE TABLE dbo.BoutiqueAccounts(Id int NOT NULL PRIMARY KEY CHECK(Id=1),Payload nvarchar(max) NOT NULL CHECK(ISJSON(Payload)=1));
        IF NOT EXISTS(SELECT 1 FROM dbo.BoutiqueAccounts WHERE Id=1)
        INSERT dbo.BoutiqueAccounts(Id,Payload) VALUES(1,N'{"users":[],"sessions":[],"tokens":[],"attempts":[]}');
        GRANT SELECT,UPDATE ON dbo.BoutiqueAccounts TO boutique_app;
        """;
    SqlConnection Connection(){var b=new SqlConnectionStringBuilder(connectionString){Encrypt=true,TrustServerCertificate=false,Pooling=false};
        if(trustLocalTestCertificate){if(!new[]{"localhost",".","(local)",Environment.MachineName}.Contains(b.DataSource,StringComparer.OrdinalIgnoreCase))throw new InvalidOperationException("Local tests only.");b.TrustServerCertificate=true;}return new(b.ConnectionString);}
    public async Task<T> RunAsync<T>(Func<JsonObject,T> action,bool write=false) {
        await using var c=Connection();await c.OpenAsync();
        await using var tx=write?(SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable):null;
        await using var read=new SqlCommand(write?"SELECT Payload FROM dbo.BoutiqueAccounts WITH(UPDLOCK,HOLDLOCK) WHERE Id=1":"SELECT Payload FROM dbo.BoutiqueAccounts WHERE Id=1",c,tx);
        var state=JsonNode.Parse((string)(await read.ExecuteScalarAsync()??throw new InvalidOperationException("Accounts have not been configured.")))!.AsObject();
        T result=default!;Exception? failure=null;
        try{result=action(state);}catch(Exception ex) when(ex is AccountDeniedException or AccountRateException){failure=ex;}
        // Failed credentials still persist rate-limit attempts. Unexpected failures roll back.
        if(write){await using var save=new SqlCommand("UPDATE dbo.BoutiqueAccounts SET Payload=@payload WHERE Id=1",c,tx);save.Parameters.Add("@payload",SqlDbType.NVarChar,-1).Value=state.ToJsonString();await save.ExecuteNonQueryAsync();await tx!.CommitAsync();}
        if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();return result;
    }
}
