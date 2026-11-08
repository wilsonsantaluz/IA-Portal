using AiGateway.Security;


using Microsoft.EntityFrameworkCore;


namespace Portal.AI.Gateway.Infrastructure;

public class PortalDbContext : DbContext
{
    private readonly OrganizationScope? _scope;
    public PortalDbContext(DbContextOptions<PortalDbContext> options, OrganizationScope? scope = null) : base(options) { _scope = scope; }
    // Explicit startup/test maintenance only; HTTP DI always supplies a scope (initially deny).
    internal bool SystemOperation { get; set; }
    public bool IsSystem => _scope is null || SystemOperation;
    public int OrganizationId => _scope?.Id ?? 1;
    public DbSet<DataConnector> DataConnectors => Set<DataConnector>();
    public DbSet<ConnectorGrant> ConnectorGrants => Set<ConnectorGrant>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();
    public DbSet<OrganizationAiDefault> OrganizationAiDefaults => Set<OrganizationAiDefault>();
    public DbSet<OrganizationAiPreference> OrganizationAiPreferences => Set<OrganizationAiPreference>();
    public DbSet<OrganizationInvitation> OrganizationInvitations => Set<OrganizationInvitation>();

    private void EnforceScope()
    {
        if (IsSystem) return;
        if (OrganizationId <= 0) throw new ChatException(403, "Organizacao nao validada.");
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            if (entry.Entity is IOrganizationEntity)
            {
                var property = entry.Property(nameof(IOrganizationEntity.OrganizationId));
                if (entry.State == EntityState.Added) {
                    if (entry.Entity is OrganizationAiDefault or OrganizationAiPreference or OrganizationInvitation && (int)property.CurrentValue! != 0 && (int)property.CurrentValue! != OrganizationId)
                        throw new ChatException(403, "Acesso entre organizacoes negado.");
                    property.CurrentValue = OrganizationId;
                }
                else if ((int)property.OriginalValue! != OrganizationId || (int)property.CurrentValue! != OrganizationId)
                    throw new ChatException(403, "Acesso entre organizacoes negado.");
            }
        }
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { EnforceScope(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    { EnforceScope(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }

    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<LlmUsage> LlmUsages => Set<LlmUsage>();
    public DbSet<AiProviderSetting> AiProviderSettings => Set<AiProviderSetting>();
    public DbSet<AiUserPreference> AiUserPreferences => Set<AiUserPreference>();
    public DbSet<AiSystemDefault> AiSystemDefaults => Set<AiSystemDefault>();
    public DbSet<KnowledgeBaseItem> KnowledgeBaseItems => Set<KnowledgeBaseItem>();
    public DbSet<ChatKbRating> ChatKbRatings => Set<ChatKbRating>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<AiProviderSetting>(e => { e.ToTable("AiProviderSettings"); e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken(); });
        modelBuilder.Entity<AiUserPreference>(e => { e.ToTable("AiUserPreferences"); e.HasKey(x => x.UserId); e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<AiSystemDefault>(e => { e.ToTable("AiSystemDefault"); e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken(); });
        modelBuilder.Entity<Organization>(e => { e.ToTable("Organizations"); e.HasKey(x => x.Id); e.HasIndex(x => x.Slug).IsUnique(); });
        modelBuilder.Entity<OrganizationInvitation>(e => {
            e.ToTable("OrganizationInvitations",t=>t.HasCheckConstraint("CK_OrganizationInvitations_Role","Role IN ('Member','Viewer')"));
            e.HasKey(x=>x.Id); e.HasIndex(x=>x.TokenHash).IsUnique(); e.HasIndex(x=>new{x.OrganizationId,x.ExpiresAt});
            e.Property(x=>x.TokenHash).HasMaxLength(64); e.Property(x=>x.Version).IsConcurrencyToken();
            e.HasOne<Organization>().WithMany().HasForeignKey(x=>x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x=>IsSystem || x.OrganizationId==OrganizationId);
        });
        modelBuilder.Entity<OrganizationMembership>(e => {
            e.ToTable("OrganizationMemberships"); e.HasKey(x => new { x.OrganizationId, x.UserId });
            e.Property(x => x.PolicyRole).HasMaxLength(20);
            e.Property(x => x.Version).IsConcurrencyToken().HasDefaultValue(0);
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<OrganizationAiDefault>(e => {
            e.ToTable("OrganizationAiDefaults"); e.HasKey(x => x.OrganizationId); e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => IsSystem || x.OrganizationId == OrganizationId);
        });
        modelBuilder.Entity<OrganizationAiPreference>(e => {
            e.ToTable("OrganizationAiPreferences"); e.HasKey(x => new { x.OrganizationId, x.UserId });
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => IsSystem || x.OrganizationId == OrganizationId);
        });
        modelBuilder.Entity<KnowledgeBaseItem>().HasQueryFilter(x => IsSystem || x.OrganizationId == OrganizationId);
        modelBuilder.Entity<Role>().HasQueryFilter(x => IsSystem || x.OrganizationId == OrganizationId);
        modelBuilder.Entity<UserRole>().HasQueryFilter(x => IsSystem || x.Role.OrganizationId == OrganizationId);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(x => IsSystem || x.Role.OrganizationId == OrganizationId);
        modelBuilder.Entity<ChatSession>().HasQueryFilter(x => IsSystem || x.OrganizationId == OrganizationId);
        modelBuilder.Entity<ChatMessage>().HasQueryFilter(x => IsSystem || x.Session.OrganizationId == OrganizationId);
        modelBuilder.Entity<LlmUsage>().HasQueryFilter(x => IsSystem || ChatSessions.Any(s => s.Id == x.SessionId && s.OrganizationId == OrganizationId));
        modelBuilder.Entity<ChatKbRating>().HasQueryFilter(x => IsSystem || x.OrganizationId == OrganizationId);
        foreach (var type in new[] { typeof(Role), typeof(KnowledgeBaseItem), typeof(ChatSession), typeof(ChatKbRating) })
        {
            modelBuilder.Entity(type).Property<int>("OrganizationId").HasDefaultValue(1);
            modelBuilder.Entity(type).HasOne(typeof(Organization), null).WithMany().HasForeignKey("OrganizationId").OnDelete(DeleteBehavior.Restrict);
        }
        modelBuilder.Entity<KnowledgeBaseItem>().HasIndex(x => new { x.OrganizationId, x.KeyName });
        modelBuilder.Entity<ChatSession>().HasIndex(x => x.OrganizationId);
        modelBuilder.Entity<ChatKbRating>().HasIndex(x => x.OrganizationId);
        modelBuilder.Entity<DataConnector>(e => {
            e.ToTable("DataConnectors"); e.HasKey(x => x.Id); e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.OrganizationId).HasDefaultValue(1); e.HasQueryFilter(x => IsSystem || x.OrganizationId == OrganizationId);
        });
        modelBuilder.Entity<ConnectorGrant>(e => {
            e.ToTable("ConnectorGrants"); e.HasKey(x => new { x.ConnectorId, x.RoleId });
            e.HasOne(x => x.Connector).WithMany().HasForeignKey(x => x.ConnectorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => IsSystem || (x.Connector.OrganizationId == OrganizationId && x.Role.OrganizationId == OrganizationId));
        });
        // USERS
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserName).HasMaxLength(150).IsRequired();
            e.Property(x => x.UserEmail).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.UserEmail).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        });

        // ROLES
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("Roles");
            e.HasKey(x => x.RoleId);
            e.Property(x => x.RoleName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.OrganizationId, x.RoleName }).IsUnique();
        });

        // USER ROLES
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("UserRoles");
            e.HasKey(x => new { x.UserId, x.RoleId });

            e.HasOne(x => x.User)
             .WithMany(x => x.Roles)
             .HasForeignKey(x => x.UserId);

            e.HasOne(x => x.Role)
             .WithMany()
             .HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("Permissions");
            e.HasKey(x => x.PermissionId);
            e.HasIndex(x => x.PermissionKey).IsUnique();
            e.Property(x => x.PermissionKey)
                .HasMaxLength(150)
                .IsRequired();
        });

        // ROLE PERMISSIONS
        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("RolePermissions");
            e.HasKey(x => new { x.RoleId, x.PermissionId });

            e.HasOne(x => x.Role)
                .WithMany()
                .HasForeignKey(x => x.RoleId);

            e.HasOne(x => x.Permission)
                .WithMany()
                .HasForeignKey(x => x.PermissionId);
        });

        modelBuilder.Entity<ChatSession>(e =>
        {
            e.ToTable("ChatSession");
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.OwnerUserId, x.DeletedAt, x.UpdatedAt, x.Id });
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.HasIndex(x => new { x.SessionId, x.CreatedAt, x.Id });
            e.HasIndex(x => new { x.SessionId, x.RequestId, x.Role }).IsUnique().HasFilter("RequestId IS NOT NULL");
            e.HasOne(x => x.Session)
             .WithMany(x => x.Messages)
             .HasForeignKey(x => x.SessionId);
        });

        modelBuilder.Entity<LlmUsage>(e =>
        {
            e.ToTable("LlmUsage");
            e.HasKey(x => x.Id);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ChatSession>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.SessionId, x.Purpose }).IsUnique().HasFilter("Purpose = 'title'");
            e.HasIndex(x => new { x.MessageId, x.Purpose }).IsUnique().HasFilter("Purpose IN ('chat', 'document')");
            e.HasIndex(x => new { x.SessionId, x.CreatedAt });
            e.HasIndex(x => new { x.Purpose, x.CreatedAt });
            e.HasIndex(x => new { x.CreatedAt, x.Id });
            e.HasIndex(x => new { x.OwnerUserId, x.CreatedAt });
            e.HasIndex(x => new { x.Provider, x.Model, x.CreatedAt });
        });

        modelBuilder.Entity<KnowledgeBaseItem>(e =>
        {
            e.ToTable("KnowledgeBaseItem");
            e.HasKey(x => x.Id);
            e.Property(x => x.KeyName).HasMaxLength(100);
            e.Property(x => x.Title).HasMaxLength(200);
        });


        modelBuilder.Entity<ChatKbRating>(e =>
        {
            e.ToTable("ChatKbRatings");
            e.HasKey(x => x.Id);
        });
    }
}
public class Role : IOrganizationEntity
{
    public int OrganizationId { get; set; } = 1;
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
}
public class ChatSession : IOrganizationEntity
{
    public int OrganizationId { get; set; } = 1;
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? OwnerUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string TitleSource { get; set; } = "provisional";
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int Version { get; set; }
    public Guid? ActiveRequestId { get; set; }
    public DateTime? LeaseUntil { get; set; }
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}
public class User
{

    public int UserId { get; set; }
    public string UserName { get; set; } = default!;
    public string UserEmail { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
}


public class UserRole
{
    public int UserId { get; set; }
    public int RoleId { get; set; }

    public User User { get; set; } = default!;
    public Role Role { get; set; } = default!;
}


public class ChatMessage
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = default!;
    public string Content { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public Guid? RequestId { get; set; }
    public string Status { get; set; } = "completed";
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? RequestFingerprint { get; set; }

    public ChatSession Session { get; set; } = default!;
}

public class KnowledgeBaseItem : IOrganizationEntity
{
    public int OrganizationId { get; set; } = 1;
    public int Id { get; set; }

    /// <summary>Identificador único curto (ex: ref1_vp_calculo).</summary>
    public string KeyName { get; set; } = default!;

    /// <summary>Seção principal (ex: "Referência 1 — Valor Presente").</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>Subseção específica dentro da seção.</summary>
    public string? Subsection { get; set; }

    /// <summary>Título amigável opcional.</summary>
    public string? Title { get; set; }

    /// <summary>Categoria opcional (mantida para compatibilidade).</summary>
    public int? CategoryId { get; set; }

    /// <summary>JSON com o vetor de embedding (debug/inspeção).</summary>
    public string? EmbeddingJson { get; set; }

    /// <summary>Nome do modelo utilizado no embedding.</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>Conteúdo em Markdown correspondente à subseção.</summary>
    public string Content { get; set; } = default!;

    /// <summary>Vetor de embedding em binário.</summary>
    public byte[]? Embedding { get; set; }

    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
public class ChatKbRating : IOrganizationEntity
{
    public int OrganizationId { get; set; } = 1;
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string Question { get; set; } = "";
    public string Sources { get; set; } = "";
    public int Rating { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? Section { get; set; }
    public string? Subsection { get; set; }
    public int? OperationId { get; set; }
}
public class LlmUsage
{
    public Guid Id { get; set; }
    public int OwnerUserId { get; set; }
    public Guid SessionId { get; set; }
    public long MessageId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public string Purpose { get; set; } = "title";
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "started";
    public string Source { get; set; } = "unavailable";
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    public long? CacheTokens { get; set; }
    public long? DurationMs { get; set; }
    public long? EstimatedCostNanoUsd { get; set; }
    public string? PriceVersion { get; set; }
    public string ApiBilling { get; set; } = "external";
}
public class AiProviderSetting
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "";
    public string ModelsJson { get; set; } = "[]";
    public string DefaultModel { get; set; } = "";
    public int Version { get; set; }
}
public class AiUserPreference { public int UserId { get; set; } public string Provider { get; set; } = ""; public string Model { get; set; } = ""; }
public class AiSystemDefault { public int Id { get; set; } public string Provider { get; set; } = ""; public string Model { get; set; } = ""; public int Version { get; set; } }

public class Permission
{
    public int PermissionId { get; set; }
    public string PermissionKey { get; set; } = default!;
    public string? Description { get; set; }
}
public class RolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    public Role Role { get; set; } = default!;
    public Permission Permission { get; set; } = default!;
}


public interface IOrganizationEntity { int OrganizationId { get; set; } }
public class Organization { public int Id { get; set; } public string Name { get; set; } = ""; public string Slug { get; set; } = ""; public bool IsActive { get; set; } = true; }
public class OrganizationMembership {
    public int OrganizationId { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; } = true;
    // Null deliberately preserves existing custom group grants (no automatic privilege gain).
    public string? PolicyRole { get; set; }
    public int Version { get; set; }
}
