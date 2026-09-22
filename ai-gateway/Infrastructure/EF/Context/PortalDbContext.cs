

using Microsoft.EntityFrameworkCore;


namespace Portal.AI.Gateway.Infrastructure;

public class PortalDbContext : DbContext
{
    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options) { }

    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<KnowledgeBaseItem> KnowledgeBaseItems => Set<KnowledgeBaseItem>();
    public DbSet<ChatKbRating> ChatKbRatings => Set<ChatKbRating>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

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
            e.HasIndex(x => x.RoleName).IsUnique();
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
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.ToTable("ChatMessage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.HasOne(x => x.Session)
             .WithMany(x => x.Messages)
             .HasForeignKey(x => x.SessionId);
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
public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = default!;
}
public class ChatSession
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; set; }
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

    public ChatSession Session { get; set; } = default!;
}

public class KnowledgeBaseItem
{
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
public class ChatKbRating
{
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

