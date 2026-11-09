using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Channels;

public static class ChannelMapping {
    public static void Configure(ModelBuilder m,PortalDbContext db) {
        m.Entity<ChannelConnection>(e=>{
            e.ToTable("ChannelConnections");e.HasKey(x=>x.Id);e.HasAlternateKey(x=>new{x.OrganizationId,x.Id});e.HasIndex(x=>new{x.Provider,x.ExternalAccountId,x.ExternalPhoneNumberId}).IsUnique();
            e.Property(x=>x.Version).IsConcurrencyToken();e.HasQueryFilter(x=>db.IsSystem || x.OrganizationId==db.OrganizationId);
            e.HasOne<Organization>().WithMany().HasForeignKey(x=>x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ChannelContact>(e=>{
            e.ToTable("ChannelContacts");e.HasKey(x=>x.Id);e.HasAlternateKey(x=>new{x.OrganizationId,x.ConnectionId,x.Id});
            e.HasIndex(x=>new{x.ConnectionId,x.ExternalId}).IsUnique();e.HasQueryFilter(x=>db.IsSystem || x.OrganizationId==db.OrganizationId);
            e.HasOne<ChannelConnection>().WithMany().HasForeignKey(x=>new{x.OrganizationId,x.ConnectionId}).HasPrincipalKey(x=>new{x.OrganizationId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ChannelConversation>(e=>{
            e.ToTable("ChannelConversations");e.HasKey(x=>x.Id);e.HasAlternateKey(x=>new{x.OrganizationId,x.ConnectionId,x.Id});
            e.HasIndex(x=>new{x.ConnectionId,x.ContactId}).IsUnique();e.Property(x=>x.Version).IsConcurrencyToken();
            e.HasOne<ChannelContact>().WithMany().HasForeignKey(x=>new{x.OrganizationId,x.ConnectionId,x.ContactId}).HasPrincipalKey(x=>new{x.OrganizationId,x.ConnectionId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x=>db.IsSystem || x.OrganizationId==db.OrganizationId);
        });
        m.Entity<ChannelMessage>(e=>{
            e.ToTable("ChannelMessages");e.HasKey(x=>x.Id);e.HasAlternateKey(x=>new{x.OrganizationId,x.Id});e.HasIndex(x=>new{x.ConnectionId,x.ExternalId}).IsUnique();
            e.HasOne<ChannelConversation>().WithMany().HasForeignKey(x=>new{x.OrganizationId,x.ConnectionId,x.ConversationId}).HasPrincipalKey(x=>new{x.OrganizationId,x.ConnectionId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x=>db.IsSystem || x.OrganizationId==db.OrganizationId);
        });
        m.Entity<MessageDeliveryEvent>(e=>{
            e.ToTable("MessageDeliveryEvents");e.HasKey(x=>x.Id);e.HasIndex(x=>new{x.MessageId,x.ExternalKey}).IsUnique();
            e.HasOne<ChannelMessage>().WithMany().HasForeignKey(x=>new{x.OrganizationId,x.MessageId}).HasPrincipalKey(x=>new{x.OrganizationId,x.Id}).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x=>db.IsSystem || x.OrganizationId==db.OrganizationId);
        });
    }
}
