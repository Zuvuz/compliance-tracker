using System.Text.Json;
using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-208: the ISMS scope in "IsmsScopes" (one per organisation, R1) and its projects in "IsmsScopeProjects"
    public class IsmsScopeConfiguration : IEntityTypeConfiguration<IsmsScope>
    {
        public void Configure(EntityTypeBuilder<IsmsScope> builder)
        {
            builder.ToTable("IsmsScopes");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();
            builder.Property(s => s.Text).IsRequired().HasMaxLength(IsmsScope.MaxTextLength);
            builder.Property(s => s.Locations).HasMaxLength(IsmsScope.MaxShortTextLength);
            builder.Property(s => s.Departments).HasMaxLength(IsmsScope.MaxShortTextLength);
            builder.Property(s => s.InternalIssues).HasMaxLength(IsmsScope.MaxTextLength);
            builder.Property(s => s.ExternalIssues).HasMaxLength(IsmsScope.MaxTextLength);

            builder.HasOne<Organization>().WithMany().HasForeignKey(s => s.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(s => s.OrganizationId).IsUnique();

            builder.HasMany(s => s.Projects).WithOne().HasForeignKey(p => p.ScopeId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(s => s.Projects).HasField("_projects").UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class ScopedProjectConfiguration : IEntityTypeConfiguration<ScopedProject>
    {
        public void Configure(EntityTypeBuilder<ScopedProject> builder)
        {
            builder.ToTable("IsmsScopeProjects");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Property(p => p.Reason).HasMaxLength(IsmsScope.MaxShortTextLength);
            builder.HasOne<Project>().WithMany().HasForeignKey(p => p.ProjectId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(p => new { p.ScopeId, p.ProjectId }).IsUnique();
        }
    }

    // US-208 R4: the interested parties in "InterestedParties", their requirements as JSON
    public class InterestedPartyConfiguration : IEntityTypeConfiguration<InterestedParty>
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        public void Configure(EntityTypeBuilder<InterestedParty> builder)
        {
            builder.ToTable("InterestedParties");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Property(p => p.Name).IsRequired().HasMaxLength(InterestedParty.MaxNameLength);
            builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20);
            builder.Ignore(p => p.IsActive);
            builder.Ignore(p => p.Handled);
            builder.Property(p => p.Requirements)
                .HasColumnType("jsonb")
                .HasConversion(
                    list => JsonSerializer.Serialize(list, Json),
                    json => JsonSerializer.Deserialize<List<PartyRequirement>>(json, Json) ?? new List<PartyRequirement>(),
                    new ValueComparer<IReadOnlyList<PartyRequirement>>(
                        (a, b) => JsonSerializer.Serialize(a, Json) == JsonSerializer.Serialize(b, Json),
                        c => JsonSerializer.Serialize(c, Json).GetHashCode(),
                        c => c.ToList()));

            builder.HasOne<Organization>().WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(p => p.OrganizationId);
        }
    }
}
