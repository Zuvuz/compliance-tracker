using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Organizations
{
    // US-189 R3: how far a measure has come, from nothing to measured and improved
    public enum Maturity
    {
        None = 0,
        AdHoc = 1,
        Defined = 2,
        Implemented = 3,
        Measured = 4
    }

    // US-189: one answer to a NIS2 gap question, for the organisation. R5: answers are only added; a new answer to the
    // same question replaces the old one, which is kept with when it was replaced, so the history can be seen.
    public class GapAnswer
    {
        public const int MaxCommentLength = 2000;
        public const int MaxCodeLength = 50;

        public Guid Id { get; private set; } = Guid.NewGuid();
        public Guid OrganizationId { get; private set; }

        // The requirement the question is about, e.g. "Art. 21(2)(d)"
        public string RequirementCode { get; private set; } = "";
        public Maturity Maturity { get; private set; }
        public string? Comment { get; private set; }

        public Guid AnsweredById { get; private set; }
        public UserRole AnsweredRole { get; private set; }
        public DateTime AnsweredAt { get; private set; }

        // Set when a newer answer replaced this one
        public DateTime? ReplacedAt { get; private set; }

        public bool IsCurrent => ReplacedAt is null;

        // For EF Core only
        private GapAnswer() { }

        public static GapAnswer Create(Guid organizationId, string requirementCode, Maturity maturity, string? comment,
            Guid answeredById, UserRole role, DateTime at)
        {
            var code = (requirementCode ?? "").Trim();
            if (code.Length == 0 || code.Length > MaxCodeLength)
            {
                throw new DomainException("Choose a question");
            }

            if (!Enum.IsDefined(maturity))
            {
                throw new DomainException("Choose a maturity from 0 to 4");
            }

            var text = comment?.Trim();
            if (text is { Length: > MaxCommentLength })
            {
                throw new DomainException($"The comment must be {MaxCommentLength} characters or fewer");
            }

            return new GapAnswer
            {
                OrganizationId = organizationId,
                RequirementCode = code,
                Maturity = maturity,
                Comment = string.IsNullOrEmpty(text) ? null : text,
                AnsweredById = answeredById,
                AnsweredRole = role,
                AnsweredAt = at
            };
        }

        public void ReplaceBy(GapAnswer newer)
        {
            if (newer.RequirementCode != RequirementCode || newer.OrganizationId != OrganizationId)
            {
                throw new DomainException("Only an answer to the same question replaces another");
            }

            ReplacedAt = newer.AnsweredAt;
        }
    }

    // US-189 R1: at least one question in plain words for each NIS2 measure. Built in, like the NIS2 requirements.
    public static class Nis2Questions
    {
        public sealed record Question(string Code, string Text);

        public static readonly IReadOnlyList<Question> All =
        [
            new("Art. 20(1)", "Has the management approved the security measures, and does it follow up on them?"),
            new("Art. 20(2)", "Has the management taken cybersecurity training, and do employees get it regularly?"),
            new("Art. 21(2)(a)", "Do you have a risk analysis and an information security policy that are kept up to date?"),
            new("Art. 21(2)(b)", "Do you have a way to detect, report and handle security incidents?"),
            new("Art. 21(2)(c)", "Do you have backups, a disaster recovery plan and a plan for crises, and are they tested?"),
            new("Art. 21(2)(d)", "Do you know your critical suppliers, and do your agreements with them cover security?"),
            new("Art. 21(2)(e)", "Is security part of buying, developing and maintaining your systems, including handling vulnerabilities?"),
            new("Art. 21(2)(f)", "Do you check whether your security measures work, e.g. with tests, audits or measurements?"),
            new("Art. 21(2)(g)", "Do all employees follow basic cyber hygiene (updates, passwords, phishing) and get training?"),
            new("Art. 21(2)(h)", "Do you have rules for when and how data is encrypted, and are they followed?"),
            new("Art. 21(2)(i)", "Do you control who has access to what, keep a list of your assets, and handle joiners and leavers?"),
            new("Art. 21(2)(j)", "Is multi-factor authentication used, and are voice, video, text and emergency communications secured?"),
            new("Art. 21(3)", "Do you consider each supplier's own vulnerabilities and security practices?"),
            new("Art. 21(4)", "When a measure is found not to work, is it corrected without delay?"),
            new("Art. 23(1)", "Do you know when and how to notify the authorities (CSIRT) and customers of a significant incident?"),
            new("Art. 23(4)(a)", "Can you send an early warning within 24 hours and a notification within 72 hours?")
        ];

        public static Question? For(string code)
            => All.FirstOrDefault(q => string.Equals(q.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

        public static string Title(Maturity maturity) => maturity switch
        {
            Maturity.None => "0 – None",
            Maturity.AdHoc => "1 – Ad hoc",
            Maturity.Defined => "2 – Defined",
            Maturity.Implemented => "3 – Implemented",
            Maturity.Measured => "4 – Measured and improved",
            _ => maturity.ToString()
        };
    }
}
