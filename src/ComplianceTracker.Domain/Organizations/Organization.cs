using ComplianceTracker.Domain.Projects;
using ComplianceTracker.Domain.Common;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Domain.Organizations
{
    // The company that uses the tracker. Its members are users with a role (US-001).
    public class Organization
    {
        // Private list, so members can only be added through methods on this class
        private readonly List<User> _users = new();

        public Guid Id { get; } = Guid.NewGuid();
        public string Name { get; private set; } = "";

        // Danish company registration number: exactly 8 digits, unique across the tracker
        public string Cvr { get; private set; } = "";

        // Sector and size decide later which frameworks are relevant (e.g. NIS2)
        public Sector Sector { get; private set; }
        public OrganizationSize Size { get; private set; }

        // US-142 R6: the EU country the organisation is established in (ISO 3166 code), which decides the national laws,
        // e.g. the Danish NIS2 law for "DK". Denmark until an admin changes it.
        public string Country { get; private set; } = DefaultCountry;
        public const string DefaultCountry = "DK";

        // US-183 R1: how many days before a deadline the reminders are sent
        public int ReminderDaysBefore { get; private set; } = Reminders.ReminderKinds.DefaultDaysBefore;

        // US-184 R3: closing waits this long and can be undone until then
        public const int ClosingWaitDays = 30;

        public DateTime? ClosingStartedAt { get; private set; }
        public Guid? ClosingStartedById { get; private set; }
        public DateOnly? ClosesOn { get; private set; }
        public DateTime? ClosedAt { get; private set; }

        public bool IsClosing => ClosesOn is not null && ClosedAt is null;

        public bool IsClosingDue(DateOnly today) => IsClosing && today >= ClosesOn;

        public void StartClosing(Guid byId, DateOnly today)
        {
            if (_users.FirstOrDefault(u => u.Id == byId)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            if (IsClosing || ClosedAt is not null)
            {
                throw new DomainException("The organisation is already being closed");
            }

            ClosingStartedAt = today.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            ClosingStartedById = byId;
            ClosesOn = today.AddDays(ClosingWaitDays);
        }

        public void UndoClosing(Guid byId)
        {
            if (_users.FirstOrDefault(u => u.Id == byId)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            if (!IsClosing)
            {
                throw new DomainException("The organisation is not being closed");
            }

            ClosingStartedAt = null;
            ClosingStartedById = null;
            ClosesOn = null;
        }

        // US-184 R3: after the data is deleted the organisation and its members are anonymised; the activity log stays
        public void Close(DateTime at)
        {
            var mark = Id.ToString("N")[..7].ToUpperInvariant();
            Name = $"Closed organisation {mark}";
            Cvr = "X" + mark;
            foreach (var user in _users)
            {
                if (user.IsActive)
                {
                    user.Remove(user.Id, at);
                }

                if (!user.IsErased)
                {
                    user.Pseudonymise();
                }
            }

            ClosedAt = at;
        }

        // US-195 R3: the default months per kind of date; changing them changes no date already set
        public int EvidenceValidityMonths { get; private set; } = ReviewDefaults.Standard.EvidenceValidity;
        public int DocumentReviewMonths { get; private set; } = ReviewDefaults.Standard.DocumentReview;
        public int SupplierEvidenceMonths { get; private set; } = ReviewDefaults.Standard.SupplierEvidence;
        public int AuditCertificateMonths { get; private set; } = ReviewDefaults.Standard.AuditCertificate;

        public ReviewDefaults ReviewDefaults => new(EvidenceValidityMonths, DocumentReviewMonths, SupplierEvidenceMonths, AuditCertificateMonths);

        public void SetReviewDefaults(ReviewDefaults defaults)
        {
            var checkedDefaults = defaults.Checked();
            EvidenceValidityMonths = checkedDefaults.EvidenceValidity;
            DocumentReviewMonths = checkedDefaults.DocumentReview;
            SupplierEvidenceMonths = checkedDefaults.SupplierEvidence;
            AuditCertificateMonths = checkedDefaults.AuditCertificate;
        }

        // US-217 R1: the roles that approve accepting a High or Critical risk; a member with one of them accepts at once.
        // Only roles that may decide compliance; Organisation admin by default.
        public List<UserRole> RiskApproverRoles { get; private set; } = [UserRole.Admin];

        public bool IsRiskApprover(UserRole role) => RiskApproverRoles.Contains(role);

        // R1, R7: returns the old roles, for the log; throws when no role or a role that may not decide compliance is chosen
        public IReadOnlyList<UserRole> SetRiskApproverRoles(IEnumerable<UserRole>? roles)
        {
            var chosen = (roles ?? []).Distinct().OrderBy(r => r).ToList();
            if (chosen.Count == 0)
            {
                throw new DomainException("Choose at least one role that approves accepting High and Critical risks");
            }

            var invalid = chosen.Where(r => !Enum.IsDefined(r) || !RolePermissions.Can(r, Permission.DecideCompliance)).ToList();
            if (invalid.Count > 0)
            {
                throw new DomainException($"{RolePermissions.Title(invalid[0])} may not decide compliance, so it cannot approve risks");
            }

            var old = RiskApproverRoles.ToList();
            RiskApproverRoles = chosen;
            return old;
        }

        // US-219 R7: the access log is off until the organisation admin turns it on with a start date at least 6 weeks
        // ahead, so the members are told first. Null is off. Nothing is logged before the start date.
        public DateOnly? AccessLogStartsOn { get; private set; }

        // US-219 R6: how long visits are kept; 12 months by default, at most 5 years
        public int AccessLogRetentionMonths { get; private set; } = ActivityLog.AccessLogRules.DefaultRetentionMonths;

        public bool IsAccessLogOn(DateOnly today) => AccessLogStartsOn is { } starts && today >= starts;

        // R7: turned on, but the start date has not come yet; the members are being told
        public bool IsAccessLogAnnounced(DateOnly today) => AccessLogStartsOn is { } starts && today < starts;

        // R6, R7: throws when the start is less than 6 weeks ahead or the retention period is outside 1 month to 5 years.
        // A log that is already on keeps its start date; only the retention period changes.
        public void TurnOnAccessLog(DateOnly startsOn, int retentionMonths, DateOnly today)
        {
            if (retentionMonths is < ActivityLog.AccessLogRules.MinRetentionMonths or > ActivityLog.AccessLogRules.MaxRetentionMonths)
            {
                throw new DomainException(
                    $"Keep the access log for {ActivityLog.AccessLogRules.MinRetentionMonths} to {ActivityLog.AccessLogRules.MaxRetentionMonths} months (at most 5 years)");
            }

            if (AccessLogStartsOn is null || IsAccessLogAnnounced(today))
            {
                var earliest = today.AddDays(ActivityLog.AccessLogRules.NoticeDays);
                if (startsOn < earliest)
                {
                    throw new DomainException($"The access log can start on {earliest:dd-MM-yyyy} at the earliest, so the members are told 6 weeks before");
                }

                AccessLogStartsOn = startsOn;
            }

            AccessLogRetentionMonths = retentionMonths;
        }

        // R7: off at once; the visits already logged are kept until their retention period ends
        public void TurnOffAccessLog() => AccessLogStartsOn = null;

        // US-216 R5: the organisation hid the guided start on the ISMS page; it comes back from the page itself
        public bool HidesIsmsGuide { get; private set; }

        public void SetIsmsGuideHidden(bool hidden) => HidesIsmsGuide = hidden;

        public void SetReminderDaysBefore(int days)
        {
            if (days < 1 || days > Reminders.ReminderKinds.MaxDaysBefore)
            {
                throw new DomainException($"Choose from 1 to {Reminders.ReminderKinds.MaxDaysBefore} days");
            }

            ReminderDaysBefore = days;
        }

        // The 27 EU member states
        public static readonly IReadOnlyList<string> EuCountries =
        [
            "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI", "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU",
            "LV", "MT", "NL", "PL", "PT", "RO", "SE", "SI", "SK"
        ];

        // Every user who has been a member, also removed ones, so names stay on history (US-090)
        public IReadOnlyList<User> Users => _users;

        // US-090: the members now: who can sign in, act and be chosen, e.g. as a risk's owner
        public IReadOnlyList<User> Members => _users.Where(u => u.IsActive).ToList();

        // US-036 R5: whether the organisation's decisions on AI suggestions may be exported to improve the AI.
        // Off until an Admin switches it on.
        public bool ShareAiFeedback { get; private set; }

        // US-130 R5: when sharing was last switched on; only decisions from then on are used. Null: shared from the start
        // (an organisation that switched it on itself before US-130).
        public DateTime? ShareAiFeedbackSince { get; private set; }

        // For EF Core only. Everyone else must use Create, so the rules always run.
        private Organization() { }

        // Validates the fields and makes the signed-in creator the first Admin (US-001, US-004).
        // The rule that a CVR is unique needs the database and lives in OrganizationService.
        public static Organization Create(string name, string cvr, Sector? sector, OrganizationSize? size, UserIdentity creator)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new DomainException("Name is required");
            }

            // IsAsciiDigit only accepts 0-9. IsDigit would also accept digits from other alphabets, e.g. "١٢٣٤٥٦٧٨".
            if (cvr.Length != 8 || !cvr.All(char.IsAsciiDigit))
            {
                throw new DomainException("CVR must be 8 digits");
            }

            if (sector is null)
            {
                throw new DomainException("Sector is required");
            }

            // US-001 R3: size is required too (US-016)
            if (size is null)
            {
                throw new DomainException("Size is required");
            }

            if (string.IsNullOrWhiteSpace(creator.ExternalId))
            {
                throw new DomainException("Creator must be signed in");
            }

            // Everything is checked before the object is built, so a half-created organisation never exists
            var organization = new Organization
            {
                // US-130 R1: shared by default; the organisation is told and can switch it off
                ShareAiFeedback = true,
                Name = name,
                Cvr = cvr,
                Sector = sector.Value,
                Size = size.Value
            };

            organization._users.Add(new User(creator.ExternalId, creator.Name, creator.Email, UserRole.Admin));

            return organization;
        }

        // US-036 R6: only an Admin may change it. Returns false when nothing changed.
        public bool SetShareAiFeedback(bool share, Guid userId, DateTime? at = null)
        {
            if (_users.FirstOrDefault(u => u.Id == userId)?.Role != UserRole.Admin)
            {
                throw new DomainException("Only an Admin can change this setting");
            }

            if (ShareAiFeedback == share)
            {
                return false;
            }

            ShareAiFeedback = share;
            if (share)
            {
                // US-130 R5: switched on again; the decisions made while it was off are never used
                ShareAiFeedbackSince = at ?? DateTime.UtcNow;
            }

            return true;
        }

        // US-080: the industry (null until an Admin has chosen one) and whether the products use AI (R2)
        public Industry? Industry { get; private set; }
        public bool ProductsUseAi { get; private set; }

        // US-080 R1, R6: only an Admin may choose; changing it switches nothing off. Returns false when nothing changed.
        public bool SetIndustry(Industry? industry, bool productsUseAi, Guid userId)
        {
            if (_users.FirstOrDefault(u => u.Id == userId && u.IsActive)?.Role != UserRole.Admin)
            {
                throw new DomainException("Only an Admin can choose the industry");
            }

            if (industry is null || !Enum.IsDefined(industry.Value))
            {
                throw new DomainException("Choose an industry");
            }

            if (Industry == industry && ProductsUseAi == productsUseAi)
            {
                return false;
            }

            Industry = industry;
            ProductsUseAi = productsUseAi;
            return true;
        }

        // US-142 R6: only an Admin may change it. Returns false when nothing changed.
        // US-027 R1-R4: an Admin corrects the name, sector and size with the rules from Create; the CVR never changes (R2).
        // Returns what changed, old and new, for the activity log; empty when nothing changed.
        public const int MaxNameLength = 200;

        public IReadOnlyList<Projects.ProjectChange> Update(string? name, Sector? sector, OrganizationSize? size, Guid userId)
        {
            if (_users.FirstOrDefault(u => u.Id == userId && u.IsActive)?.Role != UserRole.Admin)
            {
                throw new DomainException("Only an Admin can edit the organisation");
            }

            var trimmed = (name ?? "").Trim();
            if (trimmed.Length == 0)
            {
                throw new DomainException("Name is required");
            }

            if (trimmed.Length > MaxNameLength)
            {
                throw new DomainException($"The name can be at most {MaxNameLength} characters");
            }

            if (sector is null || !Enum.IsDefined(sector.Value))
            {
                throw new DomainException("Sector is required");
            }

            if (size is null || !Enum.IsDefined(size.Value))
            {
                throw new DomainException("Size is required");
            }

            var changes = new List<Projects.ProjectChange>();
            if (trimmed != Name)
            {
                changes.Add(new Projects.ProjectChange("Name", Name, trimmed));
                Name = trimmed;
            }

            if (sector.Value != Sector)
            {
                changes.Add(new Projects.ProjectChange("Sector", Sector.ToString(), sector.Value.ToString()));
                Sector = sector.Value;
            }

            if (size.Value != Size)
            {
                changes.Add(new Projects.ProjectChange("Size", Size.ToString(), size.Value.ToString()));
                Size = size.Value;
            }

            return changes;
        }

        public bool SetCountry(string? country, Guid userId)
        {
            if (_users.FirstOrDefault(u => u.Id == userId && u.IsActive)?.Role != UserRole.Admin)
            {
                throw new DomainException("Only an Admin can change the country");
            }

            var code = (country ?? "").Trim().ToUpperInvariant();
            if (!EuCountries.Contains(code))
            {
                throw new DomainException("Choose an EU country");
            }

            if (code == Country)
            {
                return false;
            }

            Country = code;
            return true;
        }

        // ---------------------------------------------------------- Members and roles (US-009, US-010)

        // US-009 R2, R5: a person joins through an invitation. A person is a member of one organisation, or more only
        // with an external role (US-187, B121); the caller checks the other organisations.
        public User AddMember(UserIdentity identity, UserRole role)
        {
            if (_users.FirstOrDefault(u => u.ExternalId == identity.ExternalId) is { } known)
            {
                if (known.IsActive)
                {
                    throw new DomainException("You are already a member of this organisation");
                }

                // US-090 R7: a removed member who is invited again comes back, with the history under the same name
                known.Restore(role);
                return known;
            }

            var user = new User(identity.ExternalId, identity.Name, identity.Email, role);
            _users.Add(user);
            return user;
        }

        // US-010 R2, R3: an Admin changes a member's role. Returns the change, or null if the role is the same.
        // Only what the member may do from now on changes; their earlier decisions keep the role they had then.
        public ProjectChange? ChangeRole(Guid memberId, UserRole role, Guid changedById)
        {
            if (_users.FirstOrDefault(u => u.Id == changedById)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            return SetRole(memberId, role);
        }

        // US-132: an Admin chooses who signs reports for the management. Returns the change, or null when nothing changed.
        public ProjectChange? SetSignsForManagement(Guid memberId, bool signs, Guid changedById)
        {
            if (_users.FirstOrDefault(u => u.Id == changedById)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            var member = Members.FirstOrDefault(u => u.Id == memberId) ?? throw new DomainException("Member not found");
            if (member.SignsForManagement == signs)
            {
                return null;
            }

            member.SignsForManagement = signs;
            return new ProjectChange("Signs for the management", signs ? "No" : "Yes", signs ? "Yes" : "No");
        }

        // US-182: the auditor who accepted a grant may only read that project, in its period
        public void LimitMember(Guid memberId, AuditorGrant grant)
        {
            var member = Members.FirstOrDefault(u => u.Id == memberId) ?? throw new DomainException("Member not found");
            if (member.Role != UserRole.Auditor)
            {
                throw new DomainException("Only an auditor's access is limited to a project");
            }

            member.LimitTo(grant.ProjectId, grant.From, grant.Until);
        }

        // US-196 R2, R3: the limits of an accepted invitation; a member invited again gets the new invitation's limits.
        // The access starts on the day it is accepted and ends after its end date; without an end date it does not end.
        public void LimitMember(Guid memberId, Invitation invitation, DateOnly today)
        {
            var member = Members.FirstOrDefault(u => u.Id == memberId) ?? throw new DomainException("Member not found");
            if (invitation.AccessUntil is null && invitation.OnlyProjectId is null)
            {
                member.LimitTo(null, null, null);
                return;
            }

            member.LimitTo(invitation.OnlyProjectId, today, invitation.AccessUntil ?? DateOnly.MaxValue);
        }

        // US-196 R5: an Admin changes the last day of a consultant's or an auditor's access. Returns the change, or null.
        public ProjectChange? SetAccessUntil(Guid memberId, DateOnly until, Guid changedById, DateOnly today)
        {
            if (_users.FirstOrDefault(u => u.Id == changedById)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            var member = Members.FirstOrDefault(u => u.Id == memberId) ?? throw new DomainException("Member not found");
            if (!RolePermissions.IsExternal(member.Role))
            {
                throw new DomainException("An end date is only for a consultant or an auditor");
            }

            if (until < today)
            {
                throw new DomainException("The end date must be today or later");
            }

            if (member.AccessUntil == until)
            {
                return null;
            }

            var before = member.AccessUntil is { } old && old != DateOnly.MaxValue ? old.ToString("dd-MM-yyyy") : "No end date";
            member.LimitTo(member.OnlyProjectId, member.AccessFrom is { } from && from <= today ? from : today, until);
            return new ProjectChange("Access until", before, until.ToString("dd-MM-yyyy"));
        }

        // US-182 R3, R4: a member who may decide compliance ends an auditor's limited access at once
        public User EndAuditorAccess(Guid memberId, Guid endedById, DateTime at)
        {
            if (_users.FirstOrDefault(u => u.Id == endedById)?.Can(Permission.DecideCompliance) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            var member = Members.FirstOrDefault(u => u.Id == memberId && u.IsLimitedToProject)
                ?? throw new DomainException("Member not found");
            member.Remove(endedById, at);
            return member;
        }

        // US-090 R1, R2: an Admin removes a member. The member is kept, so their name stays on what they did (R4).
        public User RemoveMember(Guid memberId, Guid removedById, DateTime at)
        {
            if (_users.FirstOrDefault(u => u.Id == removedById)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            var member = Members.FirstOrDefault(u => u.Id == memberId)
                ?? throw new DomainException("Member not found");

            // R2: the organisation always keeps at least one Admin (US-010 R3)
            if (member.Role == UserRole.Admin && Members.Count(u => u.Role == UserRole.Admin) == 1)
            {
                throw new DomainException("Give another member the Admin role first");
            }

            member.Remove(removedById, at);
            return member;
        }

        // US-095 R3: an Admin erases the personal data of a member who has been removed (US-090); an active member cannot
        // be erased, and erasing twice is refused
        public User ErasePersonalData(Guid memberId, Guid adminId)
        {
            if (_users.FirstOrDefault(u => u.Id == adminId)?.Can(Permission.ManageMembers) != true)
            {
                throw new DomainException("Your role does not allow this");
            }

            var member = _users.FirstOrDefault(u => u.Id == memberId) ?? throw new DomainException("Member not found");
            if (member.IsActive)
            {
                throw new DomainException("Remove the member first");
            }

            if (member.IsErased)
            {
                throw new DomainException("The member's personal data is already erased");
            }

            member.Pseudonymise();
            return member;
        }

        // US-010: the system admin (the tracker's owner) changes a role, e.g. when an organisation has locked itself out.
        // The caller checks that the person is the system admin.
        public ProjectChange? ChangeRoleAsSystemAdmin(Guid memberId, UserRole role) => SetRole(memberId, role);

        private ProjectChange? SetRole(Guid memberId, UserRole role)
        {
            if (!Enum.IsDefined(role))
            {
                throw new DomainException("Choose a role");
            }

            var member = Members.FirstOrDefault(u => u.Id == memberId)
                ?? throw new DomainException("Member not found");

            if (member.Role == role)
            {
                return null;
            }

            // R3: the organisation always keeps at least one Admin
            if (member.Role == UserRole.Admin && Members.Count(u => u.Role == UserRole.Admin) == 1)
            {
                throw new DomainException("The organisation must keep at least one organisation admin");
            }

            var change = new ProjectChange(member.Name, RolePermissions.Title(member.Role), RolePermissions.Title(role));
            member.Role = role;
            if (!RolePermissions.IsExternal(role))
            {
                member.LimitTo(null, null, null); // US-196 R2: an employee role has no end date or project limit
            }

            return change;
        }

    }
}
