using ComplianceTracker.Domain.Frameworks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComplianceTracker.Infrastructure.Persistence.Configurations
{
    // How Requirement is stored in the table "Requirements" (US-007)
    public class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
    {
        // US-142: the NIS2 catalogues and the built-in versions they belong to
        public static readonly IReadOnlyList<(IReadOnlyList<BuiltInNis2Requirements.Item> Catalogue, Guid VersionId)> Nis2Catalogues =
        [
            (BuiltInNis2Requirements.Nis2, BuiltInFrameworks.Nis2.VersionId),
            (BuiltInNis2Requirements.Danish, BuiltInFrameworks.Nis2Denmark.VersionId),
            (BuiltInNis2Requirements.Technical, BuiltInFrameworks.Nis2Technical.VersionId)
        ];

        public void Configure(EntityTypeBuilder<Requirement> builder)
        {
            builder.ToTable("Requirements");

            // US-092: PostgreSQL's xmin as the row version; a save fails if someone else changed the row since it was read
            builder.Property(x => x.Version).IsRowVersion();

            // The id is set by the domain, not by the database
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Code).IsRequired().HasMaxLength(50);
            builder.Property(r => r.Title).IsRequired().HasMaxLength(500);
            builder.Property(r => r.Description).IsRequired().HasMaxLength(4000);

            // US-043: the part of the document and the link to the official text (built-in requirements only)
            builder.Property(r => r.Group).HasMaxLength(200);
            builder.Property(r => r.Reference).HasMaxLength(500);

            // US-142 R6, R8: the directive requirement a national one implements, and the texts in other languages
            builder.Property(r => r.Implements).HasMaxLength(50);
            builder.HasMany(r => r.Texts)
                .WithOne()
                .HasForeignKey(t => t.RequirementId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Texts)
                .HasField("_texts")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .AutoInclude();

            // US-043: the CRA's Annex I comes with every database, on the built-in CRA version 1
            builder.HasData(BuiltInCraRequirements.All.Select(item => new
            {
                item.Id,
                FrameworkVersionId = BuiltInFrameworks.Cra.VersionId,
                item.Code,
                item.Title,
                item.Description,
                item.Position,
                item.Group,
                Reference = (string?)BuiltInCraRequirements.Reference
            }));

            // US-069: version 2 of the CRA
            builder.HasData(BuiltInCraRequirementsV2.All.Select(item => new
            {
                item.Id,
                FrameworkVersionId = BuiltInFrameworks.Cra.Version2Id,
                item.Code,
                item.Title,
                item.Description,
                item.Position,
                item.Group,
                Reference = (string?)item.Reference
            }));

            // US-062: the AI Act's requirements, on the built-in AI Act version 1
            builder.HasData(BuiltInAiActRequirements.All.Select(item => new
            {
                item.Id,
                FrameworkVersionId = BuiltInFrameworks.AiAct.VersionId,
                item.Code,
                item.Title,
                item.Description,
                item.Position,
                item.Group,
                Reference = (string?)item.Reference
            }));

            // US-142: NIS2, the Danish NIS2 law and Implementing Regulation (EU) 2024/2690
            foreach (var (catalogue, versionId) in Nis2Catalogues)
            {
                builder.HasData(catalogue.Select(item => new
                {
                    item.Id,
                    FrameworkVersionId = versionId,
                    item.Code,
                    item.Title,
                    item.Description,
                    item.Position,
                    item.Group,
                    Reference = (string?)item.Reference,
                    item.Implements
                }));
            }

            // A code is used once per version (the domain checks it first, the index is the safety net)
            builder.HasIndex(r => new { r.FrameworkVersionId, r.Code }).IsUnique();
        }
    }
}
