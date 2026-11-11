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
        async Task ExecuteScript(string text) {
            // Microsoft.Data.Sqlite 8 can swallow later batch errors when a reader is
            // disposed by ExecuteNonQuery. Explicitly consume every result before commit.
            using var command = connection.CreateCommand(); command.CommandText = text;
            command.Transaction = (SqliteTransaction?)db.Database.CurrentTransaction?.GetDbTransaction();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.NextResultAsync()) { }
        }
        async Task<long> Count(string query) { using var c = connection.CreateCommand(); c.CommandText = query; return Convert.ToInt64(await c.ExecuteScalarAsync()); }
        async Task<HashSet<string>> Columns(string table) {
            using var c = connection.CreateCommand(); c.CommandText = $"PRAGMA table_info(\"{table}\")";
            using var r = await c.ExecuteReaderAsync(); var result = new HashSet<string>();
            while (await r.ReadAsync()) result.Add(r.GetString(1)); return result;
        }
        async Task Backup(int version) {
            var directory = !string.IsNullOrWhiteSpace(config["SQLITE_BACKUP_DIRECTORY"]) ? config["SQLITE_BACKUP_DIRECTORY"]! : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(connection.DataSource))!, "backups");
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, $"pre-chat-v{version}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.sqlite3");
            using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());
            await backup.OpenAsync(); connection.BackupDatabase(backup);
            using var check = backup.CreateCommand(); check.CommandText = "PRAGMA integrity_check";
            if ((string?)await check.ExecuteScalarAsync() != "ok") throw new InvalidOperationException("Backup SQLite inválido.");
        }
        async Task UpgradeTitles(bool fresh, bool alreadyBackedUp) {
            using var resource2 = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.002_TitleUsage.sql")!;
            var script = (await new StreamReader(resource2).ReadToEndAsync()).Replace("\r\n", "\n");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Checksum FROM SchemaVersions WHERE Version=2";
            var recorded = await command.ExecuteScalarAsync();
            var columns = await Columns("LlmUsage");
            if (recorded is not null) {
                if (!Equals(recorded, hash) || !columns.IsSupersetOf(new[] { "Id", "OwnerUserId", "SessionId", "MessageId", "Provider", "Model", "Purpose", "CreatedAt", "Status", "Source", "InputTokens", "OutputTokens", "TotalTokens", "CacheTokens", "DurationMs" }))
                    throw new InvalidOperationException("Versão/checksum de títulos incompatível.");
                return;
            }
            if (!fresh && columns.Count > 0) throw new InvalidOperationException("Tabela de consumo sem migração reconhecida.");
            if (!fresh && !alreadyBackedUp) await Backup(2);
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!fresh) await ExecuteScript(script);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (2, {hash}, {DateTime.UtcNow})");
            using var check = connection.CreateCommand(); check.Transaction = (SqliteTransaction)tx.GetDbTransaction(); check.CommandText = "PRAGMA foreign_key_check";
            using (var reader = await check.ExecuteReaderAsync()) if (await reader.ReadAsync()) throw new InvalidOperationException("Integridade referencial inválida na migração de títulos.");
            await tx.CommitAsync();
        }
        async Task UpgradeUsage(bool fresh, bool alreadyBackedUp) {
            using var resource3 = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.003_ChatUsage.sql")!;
            var script = (await new StreamReader(resource3).ReadToEndAsync()).Replace("\r\n", "\n");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Checksum FROM SchemaVersions WHERE Version=3";
            var recorded = await command.ExecuteScalarAsync();
            var columns = await Columns("LlmUsage");
            var current = columns.Contains("EstimatedCostNanoUsd") && columns.Contains("PriceVersion");
            if (recorded is not null) {
                if (!Equals(recorded, hash) || !current) throw new InvalidOperationException("Versão/checksum de consumo incompatível.");
                return;
            }
            if (!fresh && (columns.Contains("EstimatedCostNanoUsd") || columns.Contains("PriceVersion"))) throw new InvalidOperationException("Migração parcial de consumo não reconhecida.");
            if (!fresh && !alreadyBackedUp) await Backup(3);
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!fresh) await ExecuteScript(script);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (3, {hash}, {DateTime.UtcNow})");
            await tx.CommitAsync();
        }
        async Task UpgradeProviders(bool fresh, bool alreadyBackedUp) {
            using var resource4 = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.004_AiProviders.sql")!;
            var script = (await new StreamReader(resource4).ReadToEndAsync()).Replace("\r\n", "\n");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Checksum FROM SchemaVersions WHERE Version=4";
            var recorded = await command.ExecuteScalarAsync();
            var billing = (await Columns("LlmUsage")).Contains("ApiBilling");
            var providers = (await Columns("AiProviderSettings")).IsSupersetOf(new[] { "Id", "Enabled", "Endpoint", "ModelsJson", "DefaultModel", "Version" });
            var preferences = (await Columns("AiUserPreferences")).IsSupersetOf(new[] { "UserId", "Provider", "Model" });
            var defaults = (await Columns("AiSystemDefault")).IsSupersetOf(new[] { "Id", "Provider", "Model", "Version" });
            if (recorded is not null) { if (!Equals(recorded, hash) || !billing || !providers || !preferences || !defaults) throw new InvalidOperationException("Esquema/checksum de provedores incompatível."); return; }
            if (!fresh && (billing || providers || preferences || defaults)) throw new InvalidOperationException("Migração parcial de provedores não reconhecida.");
            if (!fresh && !alreadyBackedUp) await Backup(4);
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!fresh) await ExecuteScript(script);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (4, {hash}, {DateTime.UtcNow})");
            await tx.CommitAsync();
        }
        async Task UpgradeOrganizations(bool fresh, bool alreadyBackedUp) {
            using var resource5 = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.005_Organizations.sql")!;
            var script = (await new StreamReader(resource5).ReadToEndAsync()).Replace("\r\n", "\n");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Checksum FROM SchemaVersions WHERE Version=5";
            var recorded = await command.ExecuteScalarAsync();
            var orgTables = (await Columns("Organizations")).IsSupersetOf(new[] { "Id", "Name", "Slug", "IsActive" })
                && (await Columns("OrganizationMemberships")).IsSupersetOf(new[] { "OrganizationId", "UserId", "IsActive" });
            var scopedTables = new[] { "Roles", "KnowledgeBaseItem", "ChatSession", "ChatKbRatings" };
            var columnCount = 0;
            foreach (var table in scopedTables) if ((await Columns(table)).Contains("OrganizationId")) columnCount++;
            if (recorded is not null) {
                if (!Equals(recorded, hash) || !orgTables || columnCount != 4) throw new InvalidOperationException("Esquema/checksum de organizacoes incompativel.");
                return;
            }
            if (!fresh && !alreadyBackedUp) await Backup(5);
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!orgTables && columnCount == 0) await ExecuteScript(script);
            else if (orgTables && columnCount == 4) {
                // Fresh model or fully recognized EnsureCreated baseline. No data rewrites.
                await db.Database.ExecuteSqlRawAsync("INSERT OR IGNORE INTO Organizations (Id,Name,Slug,IsActive) VALUES (1,'Organizacao padrao','default',1)");
                await db.Database.ExecuteSqlRawAsync("INSERT OR IGNORE INTO OrganizationMemberships (OrganizationId,UserId,IsActive) SELECT 1,UserId,1 FROM Users");
            }
            else throw new InvalidOperationException("Migracao parcial de organizacoes nao reconhecida.");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (5, {hash}, {DateTime.UtcNow})");
            using var check = connection.CreateCommand(); check.Transaction = (SqliteTransaction)tx.GetDbTransaction(); check.CommandText = "PRAGMA foreign_key_check";
            using (var reader = await check.ExecuteReaderAsync()) if (await reader.ReadAsync()) throw new InvalidOperationException("Integridade invalida nas organizacoes.");
            await tx.CommitAsync();
        }
        async Task UpgradeConnectors(bool fresh, bool alreadyBackedUp) {
            using var resource6 = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.006_DataConnectors.sql")!;
            var script = (await new StreamReader(resource6).ReadToEndAsync()).Replace("\r\n", "\n");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Checksum FROM SchemaVersions WHERE Version=6";
            var recorded = await command.ExecuteScalarAsync();
            var complete = (await Columns("DataConnectors")).IsSupersetOf(new[] { "Id", "OrganizationId", "ProfileId", "TablesJson", "Version" })
                && (await Columns("ConnectorGrants")).IsSupersetOf(new[] { "ConnectorId", "RoleId" });
            if (recorded is not null) {
                if (!Equals(recorded,hash) || !complete) throw new InvalidOperationException("Esquema/checksum de conectores incompativel.");
                return;
            }
            if (!fresh && !alreadyBackedUp) await Backup(6);
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!complete) await ExecuteScript(script);
            else await ExecuteScript(script[script.IndexOf("CREATE TRIGGER",StringComparison.Ordinal)..].Replace("CREATE TRIGGER ","CREATE TRIGGER IF NOT EXISTS "));
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (6,{hash},{DateTime.UtcNow})");
            await tx.CommitAsync();
        }
        async Task UpgradeWorkspace(bool fresh, bool alreadyBackedUp) {
            using var resource7 = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.007_OrganizationWorkspace.sql")!;
            var script = (await new StreamReader(resource7).ReadToEndAsync()).Replace("\r\n", "\n");
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Checksum FROM SchemaVersions WHERE Version=7";
            var recorded = await command.ExecuteScalarAsync();
            var members = await Columns("OrganizationMemberships");
            var defaults = await Columns("OrganizationAiDefaults");
            var preferences = await Columns("OrganizationAiPreferences");
            var complete = members.IsSupersetOf(new[] { "PolicyRole", "Version" }) && defaults.Contains("OrganizationId") && preferences.Contains("OrganizationId");
            if (recorded is not null) {
                if (!Equals(recorded,hash) || !complete) throw new InvalidOperationException("Esquema/checksum de workspace incompativel.");
                return;
            }
            if (!fresh && (members.Contains("PolicyRole") || members.Contains("Version") || defaults.Count > 0 || preferences.Count > 0))
                throw new InvalidOperationException("Migracao parcial de workspace nao reconhecida.");
            if (!fresh && !alreadyBackedUp) await Backup(7);
            await using var tx = await db.Database.BeginTransactionAsync();
            if (!fresh) await ExecuteScript(script);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (7,{hash},{DateTime.UtcNow})");
            using var check = connection.CreateCommand(); check.Transaction = (SqliteTransaction)tx.GetDbTransaction(); check.CommandText = "PRAGMA foreign_key_check";
            using (var reader = await check.ExecuteReaderAsync()) if (await reader.ReadAsync()) throw new InvalidOperationException("Integridade invalida no workspace.");
            await tx.CommitAsync();
        }
        async Task UpgradeInvitations(bool fresh,bool alreadyBackedUp) {
            using var resource8=typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.008_OrganizationInvitations.sql")!;
            var script=(await new StreamReader(resource8).ReadToEndAsync()).Replace("\r\n","\n");
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command=connection.CreateCommand();command.CommandText="SELECT Checksum FROM SchemaVersions WHERE Version=8";
            var recorded=await command.ExecuteScalarAsync();
            var columns=await Columns("OrganizationInvitations");
            var complete=columns.IsSupersetOf(new[]{"Id","OrganizationId","TokenHash","Role","CreatedByUserId","IssuerMembershipVersion","CreatedAt","ExpiresAt","AcceptedAt","AcceptedByUserId","RevokedAt","Version"});
            if(recorded is not null){if(!Equals(recorded,hash)||!complete)throw new InvalidOperationException("Esquema/checksum de convites incompativel.");return;}
            if(!fresh && columns.Count>0)throw new InvalidOperationException("Migracao parcial de convites nao reconhecida.");
            if(!fresh && !alreadyBackedUp)await Backup(8);
            await using var tx=await db.Database.BeginTransactionAsync();
            if(!fresh)await ExecuteScript(script);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (8,{hash},{DateTime.UtcNow})");
            using var check=connection.CreateCommand();check.Transaction=(SqliteTransaction)tx.GetDbTransaction();check.CommandText="PRAGMA foreign_key_check";
            using(var reader=await check.ExecuteReaderAsync())if(await reader.ReadAsync())throw new InvalidOperationException("Integridade invalida nos convites.");
            await tx.CommitAsync();
        }
        async Task UpgradeChannels(bool fresh,bool alreadyBackedUp) {
            using var resource9=typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.009_Channels.sql")!;
            var script=(await new StreamReader(resource9).ReadToEndAsync()).Replace("\r\n","\n");
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command=connection.CreateCommand();command.CommandText="SELECT Checksum FROM SchemaVersions WHERE Version=9";
            var recorded=await command.ExecuteScalarAsync();
            var tables=new[]{"ChannelConnections","ChannelContacts","ChannelConversations","ChannelMessages","MessageDeliveryEvents"};
            var complete=true;var any=false;
            foreach(var table in tables){
                var columns=await Columns(table);any|=columns.Count>0;
                var required=table switch {
                    "ChannelConnections"=>new[]{"Id","OrganizationId","Provider","ExternalAccountId","ExternalPhoneNumberId","DisplayName","IsEnabled","AutoReply","AgentEnabled","AgentId","KnowledgeIdsJson","RetentionDays","MonthlyLimit","Version"},
                    "ChannelContacts"=>new[]{"Id","OrganizationId","ConnectionId","ExternalId"},
                    "ChannelConversations"=>new[]{"Id","OrganizationId","ConnectionId","ContactId","HumanHandoff","LastInboundAt","UpdatedAt","Version"},
                    "ChannelMessages"=>new[]{"Id","OrganizationId","ConnectionId","ConversationId","ExternalId","Direction","Text","State","CreatedAt","NextAttemptAt","Attempts","SourcesJson","Category"},
                    _=>new[]{"Id","OrganizationId","MessageId","ExternalKey","Status","PricingCategory","Billable","CreatedAt"}
                };
                complete &= columns.IsSupersetOf(required);
            }
            if(recorded is not null){if(!Equals(recorded,hash)||!complete)throw new InvalidOperationException("Esquema/checksum de canais incompativel.");return;}
            if(!fresh && any)throw new InvalidOperationException("Migracao parcial de canais nao reconhecida.");
            if(!fresh && !alreadyBackedUp)await Backup(9);
            await using var tx=await db.Database.BeginTransactionAsync();
            if(!fresh)await ExecuteScript(script);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (9,{hash},{DateTime.UtcNow})");
            using var check=connection.CreateCommand();check.Transaction=(SqliteTransaction)tx.GetDbTransaction();check.CommandText="PRAGMA foreign_key_check";
            using(var reader=await check.ExecuteReaderAsync())if(await reader.ReadAsync())throw new InvalidOperationException("Integridade invalida nos canais.");
            await tx.CommitAsync();
        }
        async Task UpgradeSignup(bool fresh, bool alreadyBackedUp) {
            using var resource10=typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.010_PublicSignup.sql")!;
            var script=(await new StreamReader(resource10).ReadToEndAsync()).Replace("\r\n","\n");
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command=connection.CreateCommand();command.CommandText="SELECT Checksum FROM SchemaVersions WHERE Version=10";
            var recorded=await command.ExecuteScalarAsync();
            var columns=await Columns("SignupRequests");
            var complete=columns.IsSupersetOf(new[]{"TokenHash","Email","Name","PasswordHash","CreatedAt","ExpiresAt"});
            if(recorded is not null) { if(!Equals(recorded,hash)||!complete)throw new InvalidOperationException("Esquema/checksum de cadastro incompatível.");return; }
            if(!fresh && columns.Count>0)throw new InvalidOperationException("Migração parcial de cadastro não reconhecida.");
            if(!fresh && !alreadyBackedUp)await Backup(10);
            await using var tx=await db.Database.BeginTransactionAsync();
            if(!fresh)await ExecuteScript(script);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (10,{hash},{DateTime.UtcNow})");
            await tx.CommitAsync();
        }
        async Task UpgradeAudit(bool fresh,bool alreadyBackedUp) {
            using var resource11=typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream("AiGateway.Infrastructure.EF.Migrations.011_ActionAudit.sql")!;
            var script=(await new StreamReader(resource11).ReadToEndAsync()).Replace("\r\n","\n");
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
            using var command=connection.CreateCommand();command.CommandText="SELECT Checksum FROM SchemaVersions WHERE Version=11";
            var recorded=await command.ExecuteScalarAsync();
            var columns=await Columns("ActionAuditEvents");
            var complete=columns.IsSupersetOf(new[]{"Id","CreatedAt","OrganizationId","ActorUserId","ActorName","Action","Operation","Area","Method","Route","TargetId","Outcome","StatusCode","DurationMs","RequestId","RemoteAddress"});
            var triggers=await Count("SELECT COUNT(*) FROM sqlite_master WHERE type='trigger' AND name IN ('ActionAuditEvents_NoUpdate','ActionAuditEvents_NoDelete')");
            var indexes=await Count("SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND tbl_name='ActionAuditEvents' AND name IN ('IX_ActionAuditEvents_CreatedAt','IX_ActionAuditEvents_OrganizationId_Id','IX_ActionAuditEvents_ActorUserId_Id')");
            if(recorded is not null) {
                if(!Equals(recorded,hash)||!complete||triggers!=2||indexes!=3)throw new InvalidOperationException("Esquema/checksum de auditoria incompatível.");
                return;
            }
            if(!fresh && columns.Count>0)throw new InvalidOperationException("Migração parcial de auditoria não reconhecida.");
            if(!fresh && !alreadyBackedUp)await Backup(11);
            await using var tx=await db.Database.BeginTransactionAsync();
            if(!fresh)await ExecuteScript(script);
            else await ExecuteScript(script[script.IndexOf("CREATE TRIGGER",StringComparison.Ordinal)..]);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES (11,{hash},{DateTime.UtcNow})");
            await tx.CommitAsync();
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
            if (recorded is not string value || value != checksum || !current || await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version>11") != 0)
                throw new InvalidOperationException("Versão/checksum de esquema incompatível. Inicialização interrompida.");
            var hadV2 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=2") != 0;
            var hadV3 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=3") != 0;
            var hadV4 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=4") != 0;
            var hadV5 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=5") != 0;
            var hadV6 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=6") != 0;
            var hadV9 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=9") != 0;
            var hadV10 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=10") != 0;
            var hadV8 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=8") != 0;
            var hadV7 = await Count("SELECT COUNT(*) FROM SchemaVersions WHERE Version=7") != 0;
            await UpgradeTitles(false, false); await UpgradeUsage(false, !hadV2); await UpgradeProviders(false, !hadV3); await UpgradeOrganizations(false, !hadV2 || !hadV3); await UpgradeConnectors(false, !hadV2 || !hadV3 || !hadV5); await UpgradeWorkspace(false,!hadV2 || !hadV3 || !hadV4 || !hadV5 || !hadV6); await UpgradeInvitations(false,!hadV2 || !hadV3 || !hadV4 || !hadV5 || !hadV6 || !hadV7); await UpgradeChannels(false,!hadV2 || !hadV3 || !hadV4 || !hadV5 || !hadV6 || !hadV7 || !hadV8); await UpgradeSignup(false,!hadV2 || !hadV3 || !hadV4 || !hadV5 || !hadV6 || !hadV7 || !hadV8 || !hadV9); await UpgradeAudit(false,!hadV2 || !hadV3 || !hadV4 || !hadV5 || !hadV6 || !hadV7 || !hadV8 || !hadV9 || !hadV10); return;
        }
        if (partial && !current) throw new InvalidOperationException("Migração parcial desconhecida. Restaure um backup validado.");
        if (!current) await Backup(1);
        await using (var transaction = await db.Database.BeginTransactionAsync()) {
        if (!current) await ExecuteScript(sql);
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
        await UpgradeTitles(isEmpty, !current);
        await UpgradeUsage(isEmpty, true);
        await UpgradeProviders(isEmpty, true);
        await UpgradeOrganizations(isEmpty, !current);
        await UpgradeConnectors(isEmpty, true);
        await UpgradeWorkspace(isEmpty,!isEmpty);
        await UpgradeInvitations(isEmpty,!isEmpty);
        await UpgradeChannels(isEmpty,!isEmpty);
        await UpgradeSignup(isEmpty,!isEmpty);
        await UpgradeAudit(isEmpty,!isEmpty);
    }
}
