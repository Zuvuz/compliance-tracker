using ComplianceTracker.Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Organization (and its list of users) is stored in the table "Organizations"
    public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
    {
        public void Configure(EntityTypeBuilder<Organization> builder)
        {
            builder.ToTable("Organizations");

            // US-090: the current members are worked out from Users, not stored
            builder.Ignore(o => o.Members);

            // The id is set by the domain (Guid.NewGuid), not by the database
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();

            builder.Property(o => o.Name).IsRequired();

            builder.Property(o => o.Cvr).IsRequired().HasMaxLength(8);
            // Safety net in the database behind the check in OrganizationService
            builder.HasIndex(o => o.Cvr).IsUnique();

            // Enums are stored as text, so new values never shift existing data
            builder.Property(o => o.Sector).HasConversion<string>().HasMaxLength(50);
            builder.Property(o => o.Industry).HasConversion<string>().HasMaxLength(20); // US-080
            builder.Property(o => o.Size).HasConversion<string>().HasMaxLength(50);

            // US-142 R6
            builder.Property(o => o.Country).IsRequired().HasMaxLength(2).HasDefaultValue(Organization.DefaultCountry);

            // US-183 R1
            builder.Property(o => o.ReminderDaysBefore).HasDefaultValue(Domain.Reminders.ReminderKinds.DefaultDaysBefore);

            // US-195 R3
            builder.Property(o => o.EvidenceValidityMonths).HasDefaultValue(ReviewDefaults.Standard.EvidenceValidity);
            builder.Property(o => o.DocumentReviewMonths).HasDefaultValue(ReviewDefaults.Standard.DocumentReview);
            builder.Property(o => o.SupplierEvidenceMonths).HasDefaultValue(ReviewDefaults.Standard.SupplierEvidence);
            builder.Property(o => o.AuditCertificateMonths).HasDefaultValue(ReviewDefaults.Standard.AuditCertificate);
            builder.Ignore(o => o.ReviewDefaults);

            // US-217 R1: the roles that approve accepting High and Critical risks, as role names; existing organisations get
            // Organisation admin
            builder.Property(o => o.RiskApproverRoles)
                .HasConversion(
                    roles => roles.Select(r => r.ToString()).ToArray(),
                    names => names.Select(n => Enum.Parse<Domain.Users.UserRole>(n)).ToList(),
                    new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<Domain.Users.UserRole>>(
                        (a, b) => a!.SequenceEqual(b!), c => c.Aggregate(0, (h, r) => HashCode.Combine(h, r)), c => c.ToList()))
                .HasColumnType("text[]")
                .HasDefaultValueSql("ARRAY['Admin']::text[]");

            // The users belong to the organisation. The foreign key only exists in the database
            // (a "shadow property"), so the User class in the domain does not need it.
            builder.HasMany(o => o.Users)
                .WithOne()
                .HasForeignKey("OrganizationId")
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            // EF writes straight into the private field _users, because Users is read-only.
            // AutoInclude loads the users every time an organisation is loaded.
            builder.Navigation(o => o.Users)
                .HasField("_users")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();
        }
    }
}
