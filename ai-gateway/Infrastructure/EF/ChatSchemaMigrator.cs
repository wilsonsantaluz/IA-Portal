using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace Portal.AI.Gateway.Infrastructure;

public static class ChatSchemaMigrator
{
    public static async Task InitializeAsync(PortalDbContext db, IConfiguration config)
    {
        using var resource = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.001_ChatSessions.sql")!;
        var sql = (await new StreamReader(resource).ReadToEndAsync()).Replace("\r\n", "\n");
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
        await db.Database.OpenConnectionAsync();
        var connection = (SqliteConnection)db.Database.GetDbConnection();
        async Task<long> Count(string query) { using var c = connection.CreateCommand(); c.CommandText = query; return Convert.ToInt64(await c.ExecuteScalarAsync()); }
        async Task<HashSet<string>> Columns(string table) {
            using var c = connection.CreateCommand(); c.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var r = await c.ExecuteReaderAsync(); var result = new HashSet<string>();
            while (await r.ReadAsync()) result.Add(r.GetString(1)); return result;
        }
        var isEmpty = await Count("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'") == 0;
        if (isEmpty) await db.Database.EnsureCreatedAsync();
        var expected = new Dictionary<string, string[]> {
            ["Users"] = ["UserId", "UserName", "UserEmail", "PasswordHash", "IsActive", "CreatedAt"],
            ["Roles"] = ["RoleId", "RoleName"], ["UserRoles"] = ["UserId", "RoleId"],
            ["Permissions"] = ["PermissionId", "PermissionKey", "Description"], ["RolePermissions"] = ["RoleId", "PermissionId"],
            ["ChatSession"] = ["Id", "UserId", "Title", "CreatedAt"],
            ["ChatMessage"] = ["Id", "SessionId", "Role", "Content", "CreatedAt"],
            ["KnowledgeBaseItem"] = ["Id", "KeyName", "Section", "Subsection", "Title", "CategoryId", "EmbeddingJson", "EmbeddingModel", "Content", "Embedding", "IsActive", "CreatedAt"],
            ["ChatKbRatings"] = ["Id", "UserId", "Question", "Sources", "Rating", "CreatedAt", "Section", "Subsection", "OperationId"] };
        foreach (var item in expected) if (! (await Columns(item.Key)).IsSupersetOf(item.Value))
            throw new InvalidOperationException("Esquema SQLite incompatível. Migração interrompida sem recriar o banco.");
        if (await Count("SELECT COUNT(*) FROM pragma_table_info('ChatSession') WHERE name='Id' AND pk=1 AND type='TEXT'") != 1
            || await Count("SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name='UserId' AND pk=1 AND type='INTEGER'") != 1
            || await Count("SELECT COUNT(*) FROM pragma_foreign_key_list('ChatMessage') WHERE \"table\"='ChatSession' AND \"from\"='SessionId' AND \"to\"='Id'") != 1)
            throw new InvalidOperationException("Chaves do esquema legado incompatíveis; migração interrompida.");
        var sessionColumns = await Columns("ChatSession");
        var messageColumns = await Columns("ChatMessage");
        string[] sessionAdditions = ["OwnerUserId", "UpdatedAt", "TitleSource", "Provider", "Model", "DeletedAt", "Version", "ActiveRequestId", "LeaseUntil"];
        string[] messageAdditions = ["RequestId", "Status", "Provider", "Model", "RequestFingerprint"];
        var current = sessionColumns.IsSupersetOf(sessionAdditions) && messageColumns.IsSupersetOf(messageAdditions);
        var partial = sessionAdditions.Any(sessionColumns.Contains) || messageAdditions.Any(messageColumns.Contains);
        var ledger = await Count("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersions'") != 0;
        if (ledger) {
            using var c = connection.CreateCommand(); c.CommandText = "SELECT Checksum FROM SchemaVersions WHERE Version=1";
            var recorded = await c.ExecuteScalarAsync();
            if (recorded is not string value || value != checksum || !current || await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version>1") != 0)
                throw new InvalidOperationException("Versão/checksum de esquema incompatível. Inicialização interrompida.");
            return;
        }
        if (partial && !current) throw new InvalidOperationException("Migração parcial desconhecida. Restaure um backup validado.");
        if (!current) {
            var directory = !string.IsNullOrWhiteSpace(config["SQLITE_BACKUP_DIRECTORY"]) ? config["SQLITE_BACKUP_DIRECTORY"]! : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(connection.DataSource))!, "backups");
            Directory.CreateDirectory(directory);
            var backupFile = Path.Combine(directory, $"pre-chat-v1-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.sqlite3");
            using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupFile, Pooling = false }.ToString());
            await backup.OpenAsync(); connection.BackupDatabase(backup);
            using var check = backup.CreateCommand(); check.CommandText = "PRAGMA integrity_check";
            if ((string?)await check.ExecuteScalarAsync() != "ok") throw new InvalidOperationException("Backup SQLite não passou na verificação de integridade.");
        }
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!current) await db.Database.ExecuteSqlRawAsync(sql);
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE SchemaVersions (Version INTEGER PRIMARY KEY, Checksum TEXT NOT NULL, AppliedAt TEXT NOT NULL)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (1, {checksum}, {DateTime.UtcNow})");
        using (var check = connection.CreateCommand()) {
            check.Transaction = (SqliteTransaction)transaction.GetDbTransaction();
            check.CommandText = "PRAGMA foreign_key_check";
            using var reader = await check.ExecuteReaderAsync();
            if (await reader.ReadAsync()) throw new InvalidOperationException("Integridade referencial inválida; migração revertida.");
        }
        await transaction.CommitAsync();
    }
}
