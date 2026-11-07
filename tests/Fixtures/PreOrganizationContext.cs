using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;
// Real pre-organization schema, not a current model with a few tables dropped.
sealed class PreOrganizationContext(DbContextOptions<PortalDbContext> options) : PortalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Ignore<ConnectorGrant>(); model.Ignore<DataConnector>();
        model.Ignore<OrganizationMembership>(); model.Ignore<Organization>();
        foreach (var type in new[] { typeof(Role), typeof(KnowledgeBaseItem), typeof(ChatSession), typeof(ChatKbRating) })
            model.Entity(type).Ignore("OrganizationId");
        foreach (var type in new[] { typeof(Role), typeof(KnowledgeBaseItem), typeof(ChatSession), typeof(ChatKbRating), typeof(ChatMessage), typeof(UserRole), typeof(RolePermission), typeof(LlmUsage) })
            model.Entity(type).HasQueryFilter(null);
        model.Entity<Role>().HasIndex(x => x.RoleName).IsUnique();
    }
}
