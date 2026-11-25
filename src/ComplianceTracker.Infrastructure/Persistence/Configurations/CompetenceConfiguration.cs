using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Organizations;
using ComplianceTracker.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // US-213: competences (the roles that need them as a text array), people without a user, training records and policy
    // confirmations
    public class CompetenceConfiguration : IEntityTypeConfiguration<Competence>
    {
        public void Configure(EntityTypeBuilder<Competence> builder)
        {
            builder.ToTable("Competences");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Property(c => c.Name).IsRequired().HasMaxLength(Competence.MaxNameLength);
            builder.Property(c => c.RequiredRoles)
                .HasConversion(
                    roles => roles.Select(r => r.ToString()).ToArray(),
                    names => names.Select(n => Enum.Parse<UserRole>(n)).ToList(),
                    new ValueComparer<List<UserRole>>((a, b) => a!.SequenceEqual(b!), c => c.Aggregate(0, (h, r) => HashCode.Combine(h, r)), c => c.ToList()))
                .HasColumnType("text[]");
            builder.Ignore(c => c.IsActive);
            builder.HasOne<Organization>().WithMany().HasForeignKey(c => c.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(c => c.OrganizationId);
        }
    }

    public class StaffPersonConfiguration : IEntityTypeConfiguration<StaffPerson>
    {
        public void Configure(EntityTypeBuilder<StaffPerson> builder)
        {
            builder.ToTable("StaffPeople");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.Property(p => p.Name).IsRequired().HasMaxLength(StaffPerson.MaxNameLength);
            builder.Ignore(p => p.IsActive);
            builder.HasOne<Organization>().WithMany().HasForeignKey(p => p.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(p => p.OrganizationId);
        }
    }

    public class TrainingRecordConfiguration : IEntityTypeConfiguration<TrainingRecord>
    {
        public void Configure(EntityTypeBuilder<TrainingRecord> builder)
        {
            builder.ToTable("TrainingRecords");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.Property(t => t.Evidence).HasMaxLength(TrainingRecord.MaxEvidenceLength);
            builder.HasOne<Organization>().WithMany().HasForeignKey(t => t.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<Competence>().WithMany().HasForeignKey(t => t.CompetenceId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(t => t.OrganizationId);
        }
    }

    public class PolicyAcknowledgementConfiguration : IEntityTypeConfiguration<PolicyAcknowledgement>
    {
        public void Configure(EntityTypeBuilder<PolicyAcknowledgement> builder)
        {
            builder.ToTable("PolicyAcknowledgements");
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedNever();
            builder.Property(a => a.Version).IsRequired().HasMaxLength(50);
            builder.HasOne<Organization>().WithMany().HasForeignKey(a => a.OrganizationId).OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(a => new { a.DocumentId, a.UserId, a.Version }).IsUnique();
            builder.HasIndex(a => a.OrganizationId);
        }
    }
}
