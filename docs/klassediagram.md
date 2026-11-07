# Compliance-tracker – Klassediagram

Senest opdateret: 2026-10-28

Klassediagrammet bygger på [domænemodellen](domaenemodel.md) og viser modellagets klasser med attributter, datatyper og associationer. **Metoder tilføjes løbende**, når en user story kræver dem, og opdateres i samme pull request som koden. Reglerne, som metoderne skal håndhæve, står i domænemodellen.

Koden er på engelsk; se ordlisten nederst.

## Notation

| Tegn | Betydning | I C# |
|---|---|---|
| `-` | privat | `private` felt (eller property med `private set`) |
| `+` | offentlig | `public` |
| `Type?` | kan være tom | nullable type |
| `$` | statisk | `static` metode |
| `-->` | har et felt af typen | felt eller property |
| `..>` | bruger typen | parameter, returtype eller lokal variabel |

## Diagram

```mermaid
classDiagram
  direction TB

  %% ================= Organisation og brugere =================
  class Organization {
    -shareAiFeedback: bool
    -shareAiFeedbackSince: DateTime?
    -id: Guid
    -name: string
    -cvr: string
    -sector: Sector
    -size: OrganizationSize
    -users: List~User~
    -projects: List~Project~
    -controls: List~Control~
    -frameworks: List~OrganizationFramework~
    +Create(name: string, cvr: string, sector: Sector?, size: OrganizationSize?, creator: UserIdentity)$ Organization
    +Members: List~User~
    +RemoveMember(memberId: Guid, removedById: Guid, at: DateTime) User
  }
  class User {
    -id: Guid
    -organizationId: Guid
    -externalId: string
    -name: string
    -email: string
    -role: UserRole
    -removedAt: DateTime?
    -removedById: Guid?
    +IsActive: bool
  }
  class UserIdentity {
    <<record>>
    +ExternalId: string
    +Name: string
    +Email: string
  }

  %% ================= Services og repositories =================
  class ICurrentUser {
    <<interface>>
    +Identity: UserIdentity
  }
  class CurrentMember {
    -currentUser: ICurrentUser
    -organizations: IOrganizationRepository
    +FindOrganizationAsync() Task~Organization?~
    +RequireAsync() Task~(Organization, User)~
    +Reset()
  }
  class IActivityLogger {
    <<interface>>
    +LogAsync(action: ActivityAction, subjectId: Guid?) Task
    +LogAsync(action: ActivityAction, subjectId: Guid?, details: string?) Task
    +LogAsync(organizationId: Guid, userId: Guid, action: ActivityAction, subjectId: Guid?) Task
  }
  class ActivityLogger {
    -log: IActivityLogRepository
    -member: CurrentMember
    -clock: TimeProvider
  }
  class OrganizationService {
    -organizations: IOrganizationRepository
    -currentUser: ICurrentUser
    -member: CurrentMember
    -log: IActivityLogger
    -unitOfWork: IUnitOfWork
    +CreateAsync(name: string, cvr: string, sector: Sector?, size: OrganizationSize?) Task~Organization~
  }
  class UserActivityService {
    -organizations: IOrganizationRepository
    -member: CurrentMember
    -log: IActivityLogger
    -unitOfWork: IUnitOfWork
    +RecordSignInAsync(externalId: string) Task
    +RecordSignOutAsync() Task
  }
  class FrameworkService {
    -frameworks: IFrameworkRepository
    -projects: IProjectRepository
    -extractor: IRequirementExtractor
    -member: CurrentMember
    -log: IActivityLogger
    -unitOfWork: IUnitOfWork
    -clock: TimeProvider
    +AddAsync(name: string, fileName: string, content: byte[]) Task~Framework~
    +UploadVersionAsync(frameworkId: Guid, fileName: string, content: byte[]) Task~FrameworkVersion~
    +AddRequirementAsync(frameworkVersionId: Guid, code: string, title: string, description: string?) Task
    +ChangeRequirementAsync(frameworkVersionId: Guid, requirementId: Guid, code: string, title: string, description: string?) Task
    +RemoveRequirementAsync(frameworkVersionId: Guid, requirementId: Guid) Task
    +SetAppliesFromAsync(frameworkVersionId: Guid, appliesFrom: DateOnly?) Task
    +ActivateAsync(frameworkVersionId: Guid) Task
    +EnableBuiltInAsync(frameworkId: Guid) Task
    +DisableBuiltInAsync(frameworkId: Guid) Task
  }
  class OrganizationQueries {
    -member: CurrentMember
    +MineAsync() Task~Organization?~
    +GetAsync(id: Guid) Task~Organization?~
  }
  class FrameworkQueries {
    -frameworks: IFrameworkRepository
    -member: CurrentMember
    +ListAsync() Task~IReadOnlyList~Framework~~
    +GetOwnAsync(frameworkId: Guid) Task~Framework?~
    +GetVersionAsync(frameworkVersionId: Guid) Task
    +GetDocumentAsync(frameworkVersionId: Guid) Task
    +BuiltInAsync() Task
  }
  class ProjectQueries {
    -projects: IProjectRepository
    -log: IActivityLogRepository
    -member: CurrentMember
    +ListAsync() Task~IReadOnlyList~Project~~
    +GetAsync(projectId: Guid) Task~Project?~
    +ActivityAsync(projectId: Guid) Task~IReadOnlyList~ActivityLogEntry~~
  }
  class IRequirementExtractor {
    <<interface>>
    +Extract(pdf: byte[]) IReadOnlyList~ExtractedRequirement~
  }
  class PdfRequirementExtractor
  class IFrameworkRepository {
    <<interface>>
    +NameExistsAsync(organizationId: Guid, name: string) Task~bool~
    +AddAsync(framework: Framework) Task
    +AddDocumentAsync(frameworkVersionId: Guid, content: byte[]) Task
    +GetAsync(organizationId: Guid, frameworkId: Guid) Task~Framework?~
    +GetByVersionWithRequirementsAsync(organizationId: Guid, frameworkVersionId: Guid) Task~Framework?~
    +ListAsync(organizationId: Guid) Task~IReadOnlyList~Framework~~
    +GetAvailableAsync(organizationId: Guid, frameworkId: Guid) Task~Framework?~
    +GetDocumentAsync(organizationId: Guid, frameworkVersionId: Guid) Task
    +ListBuiltInAsync() Task~IReadOnlyList~Framework~~
    +GetBuiltInAsync(frameworkId: Guid) Task~Framework?~
    +GetActiveSelectionAsync(organizationId: Guid, frameworkVersionId: Guid) Task~OrganizationFramework?~
    +AddSelectionAsync(selection: OrganizationFramework) Task
  }
  class ProjectService {
    -projects: IProjectRepository
    -frameworks: IFrameworkRepository
    -member: CurrentMember
    -log: IActivityLogger
    -unitOfWork: IUnitOfWork
    -clock: TimeProvider
    -carryOver: VersionCarryOver
    +CreateAsync(name: string, description: string?, intendedPurpose: string, scope: string?, phase: LifecyclePhase, frameworkIds: Guid[]?) Task~Project~
    +AddFrameworkAsync(projectId: Guid, frameworkId: Guid, frameworkVersionId: Guid?) Task
    +UseNewestVersionAsync(projectId: Guid, frameworkId: Guid) Task
    +RemoveFrameworkAsync(projectId: Guid, frameworkId: Guid) Task
    +UpdateAsync(projectId: Guid, name: string, description: string?, intendedPurpose: string, scope: string?) Task
    +ChangePhaseAsync(projectId: Guid, phase: LifecyclePhase, supportEndsOn: DateOnly?, supportReason: string?) Task
    +SetSupportPeriodAsync(projectId: Guid, endsOn: DateOnly, reason: string?) Task
    +ChangeRequirementMethodsAsync(projectId: Guid, methods: RequirementMethods) Task
  }
  class IProjectRepository {
    <<interface>>
    +NameExistsAsync(organizationId: Guid, name: string, exceptProjectId: Guid?) Task~bool~
    +AddAsync(project: Project) Task
    +GetAsync(organizationId: Guid, projectId: Guid) Task~Project?~
    +ListAsync(organizationId: Guid) Task~IReadOnlyList~Project~~
    +ListUsingFrameworkAsync(organizationId: Guid, frameworkId: Guid) Task~IReadOnlyList~Project~~
  }
  class IOrganizationRepository {
    <<interface>>
    +CvrExistsAsync(cvr: string) Task~bool~
    +AddAsync(organization: Organization) Task
    +GetByIdAsync(id: Guid) Task~Organization?~
    +GetByMemberAsync(externalId: string) Task~Organization?~
  }
  class IActivityLogRepository {
    <<interface>>
    +AppendAsync(entry: ActivityLogEntry) Task
    +ListAboutAsync(organizationId: Guid, subjectId: Guid) Task~IReadOnlyList~ActivityLogEntry~~
    +ListInOrderWrittenAsync(organizationId: Guid) Task~IReadOnlyList~ActivityLogEntry~~
  }
  class IUnitOfWork {
    <<interface>>
    +SaveChangesAsync() Task
  }

  %% ================= Projekt og releases =================
  class Project {
    -excludedFromAiSharing: bool
    -id: Guid
    -version: uint
    -organizationId: Guid
    -name: string
    -description: string
    -intendedPurpose: string
    -scope: string
    -phase: LifecyclePhase
    -requirementMethods: RequirementMethods
    -supportEndsOn: DateOnly?
    -supportPeriodReason: string?
    -releasedOn: DateOnly?
    -endOfSupportOn: DateOnly?
    -lastPlacedOnMarketOn: DateOnly?
    -retainUntil: DateOnly?
    -releases: List~Release~
    -workItems: List~WorkItem~
    -frameworks: List~ProjectFramework~
    +Frameworks: IEnumerable~ProjectFramework~
    +IsClosed: bool
    +Create(organizationId: Guid, name: string, description: string?, intendedPurpose: string, scope: string?, phase: LifecyclePhase)$ Project
    +FrameworkInUse(frameworkId: Guid) ProjectFramework?
    +UseFramework(framework: Framework, version: FrameworkVersion, userId: Guid, at: DateTime) ProjectFramework
    +ChangeFrameworkVersion(framework: Framework, version: FrameworkVersion, userId: Guid, at: DateTime) bool
    +RemoveFramework(framework: Framework, userId: Guid, at: DateTime)
    +Update(name: string, description: string?, intendedPurpose: string, scope: string?) IReadOnlyList~ProjectChange~
    +ChangePhase(phase: LifecyclePhase, today: DateOnly?) ProjectChange?
    +SetSupportPeriod(endsOn: DateOnly, reason: string?, today: DateOnly) ProjectChange?
    +ChangeRequirementMethods(methods: RequirementMethods) ProjectChange?
    +EndSupport(lastPlacedOnMarket: DateOnly, keepUntil: DateOnly?, today: DateOnly) ProjectChange
    +EnsureMayBeRemoved(today: DateOnly)
  }
  class ProjectChange {
    <<record>>
    +Field: string
    +OldValue: string
    +NewValue: string
  }
  class Release {
    -id: Guid
    -projectId: Guid
    -version: string
    -releaseDate: DateOnly?
    -status: ReleaseStatus
  }

  %% ================= Frameworks og krav =================
  class Framework {
    -id: Guid
    -ownerOrganizationId: Guid?
    -name: string
    -scope: FrameworkScope
    -versions: List~FrameworkVersion~
    +LatestVersion: FrameworkVersion
    +CurrentVersion(today: DateOnly) FrameworkVersion?
    +UpcomingVersion(today: DateOnly) FrameworkVersion?
    +IsBuiltIn: bool
    +RequireCurrentVersion(today: DateOnly) FrameworkVersion
    +Version(versionId: Guid) FrameworkVersion
    +Create(organizationId: Guid, name: string, file: CatalogFile, uploadedById: Guid, uploadedAt: DateTime)$ Framework
    +AddVersion(file: CatalogFile, uploadedById: Guid, uploadedAt: DateTime) FrameworkVersion
  }
  class FrameworkVersion {
    -id: Guid
    -frameworkId: Guid
    -number: int
    -fileName: string
    -sizeInBytes: long
    -sha256: string
    -uploadedById: Guid?
    -uploadedAt: DateTime
    -source: CatalogSource
    -sourceReference: string?
    -status: FrameworkVersionStatus
    -activatedAt: DateTime?
    -activatedById: Guid?
    -appliesFrom: DateOnly?
    -requirements: List~Requirement~
    +IsActive: bool
    +AppliesOn(today: DateOnly) bool
    +IsUpcomingOn(today: DateOnly) bool
    +SetAppliesFrom(appliesFrom: DateOnly?)
    +AddRequirement(code: string, title: string, description: string?) Requirement
    +ChangeRequirement(requirementId: Guid, code: string, title: string, description: string?)
    +RemoveRequirement(requirementId: Guid)
    +Activate(userId: Guid, at: DateTime)
  }
  class CatalogFile {
    <<value object>>
    +FileName: string
    +Content: byte[]
    +Sha256: string
    +Create(fileName: string, content: byte[])$ CatalogFile
  }
  class Requirement {
    -id: Guid
    -version: uint
    -frameworkVersionId: Guid
    -code: string
    -title: string
    -description: string
    -position: int
    -group: string?
    -reference: string?
    +HasSameTextAs(other: Requirement) bool
  }
  class RequirementApplicability {
    -id: Guid
    -projectId: Guid
    -requirementId: Guid
    -reason: string
    -decidedById: Guid
    -decidedAt: DateTime
    -revokedById: Guid?
    -revokedAt: DateTime?
    -needsReview: bool
    -carriedFromRequirementId: Guid?
    -reviewedById: Guid?
    -reviewedAt: DateTime?
    +IsActive: bool
    +NotApplicable(projectId: Guid, requirementId: Guid, reason: string, userId: Guid, at: DateTime)$ RequirementApplicability
    +CarryTo(requirementId: Guid, textChanged: bool) RequirementApplicability
    +MarkReviewed(userId: Guid, at: DateTime)
    +Revoke(userId: Guid, at: DateTime)
  }
  class OrganizationFramework {
    -id: Guid
    -organizationId: Guid
    -frameworkVersionId: Guid
    -enabledAt: DateTime
    -enabledById: Guid
    -disabledAt: DateTime?
    -disabledById: Guid?
    +IsActive: bool
    +Enable(organizationId: Guid, frameworkVersionId: Guid, enabledById: Guid, enabledAt: DateTime)$ OrganizationFramework
    +Disable(disabledById: Guid, disabledAt: DateTime)
  }
  class RequirementMethods {
    <<enumeration>>
    GitHubUserStories
    RequirementSpecification
    UseCases
  }
  Project --> RequirementMethods
  class ProjectFramework {
    -id: Guid
    -projectId: Guid
    -frameworkId: Guid
    -frameworkVersionId: Guid
    -addedAt: DateTime
    -addedById: Guid
    -removedAt: DateTime?
    -removedById: Guid?
    +IsActive: bool
  }

  %% ================= Kontroller og evidens =================
  class Control {
    -id: Guid
    -organizationId: Guid
    -ownerId: Guid
    -name: string
    -description: string
    -requirements: List~RequirementControl~
    -evidence: List~Evidence~
  }
  class RequirementControl {
    -requirementId: Guid
    -controlId: Guid
    -source: MappingSource
    -rationale: string
    -createdAt: DateTime
  }
  class Evidence {
    -id: Guid
    -projectId: Guid
    -title: string
    -url: string?
    -fileName: string?
    -contentType: string?
    -sizeInBytes: long?
    -sha256: string?
    -validUntil: DateOnly?
    -addedById: Guid
    -addedRole: UserRole?
    -addedAt: DateTime
    -removedById: Guid?
    -removedAt: DateTime?
    +IsActive: bool
    +IsFile: bool
    +ForLink(projectId, requirementId, title, url, validUntil, addedById, role, at)$ Evidence
    +ForFile(projectId, requirementId, title, file: EvidenceFile, validUntil, addedById, role, at)$ Evidence
    +CarryTo(requirementId: Guid) Evidence
    +IsExpiredOn(today: DateOnly) bool
    +Remove(userId: Guid, at: DateTime)
  }
  class EvidenceFile {
    <<value object>>
    +FileName: string
    +ContentType: string
    +Content: byte[]
    +Sha256: string
    +Create(fileName: string, content: byte[])$ EvidenceFile
  }

  %% ================= Vurderinger =================
  class RequirementAssessment {
    -id: Guid
    -requirementId: Guid
    -releaseId: Guid?
    -organizationId: Guid?
    -status: AssessmentStatus
    -rationale: string
    -assessedAt: DateTime
    -assessedById: Guid
  }

  %% ================= Arbejdselementer og AI =================
  class WorkItem {
    -id: Guid
    -projectId: Guid
    -releaseId: Guid?
    -type: WorkItemType
    -source: WorkItemSource
    -externalReference: string
    -url: string
    -title: string
    -rules: string
    -examples: string
    -gherkin: string
    -description: string
    -acceptanceCriteria: string
    -useCase: UseCaseSections?
    -labels: string
    -state: WorkItemState
    -documentId: Guid?
    -documentVersion: int?
    -removedInVersion: int?
    -firstSyncedAt: DateTime
    -lastSyncedAt: DateTime
    +Create(projectId: Guid, source: WorkItemSource, item: ExternalWorkItem, at: DateTime)$ WorkItem
    +FromDocument(projectId: Guid, documentId: Guid, type: WorkItemType, item: DocumentWorkItem, version: int, at: DateTime)$ WorkItem
    +Update(item: ExternalWorkItem, at: DateTime) bool
    +UpdateFromDocument(item: DocumentWorkItem, version: int, at: DateTime) bool
    +MarkRemoved() bool
    +MarkRemovedIn(version: int) bool
  }
  class RequirementDocument {
    -id: Guid
    -projectId: Guid
    -name: string
    -createdAt: DateTime
    -kind: DocumentKind
    -idPattern: string?
    -versions: List~RequirementDocumentVersion~
    +NextVersionNumber: int
    +Create(projectId: Guid, name: string, at: DateTime)$ RequirementDocument
    +AddVersion(file: RequirementFile, counts: ImportCounts, skipped: string[], userId: Guid, at: DateTime, idPattern: IdPattern?) RequirementDocumentVersion
  }
  class UseCaseSections {
    <<value object>>
    -primaryActor: string
    -preconditions: string
    -steps: List~string~
    -extensions: string
    -postconditions: string
    +AcceptanceCriteria: string
  }
  class DocumentKind {
    <<enumeration>>
    RequirementSpecification
    UseCases
  }
  class IdPattern {
    <<value object>>
    +Default: string$
    -value: string
    +Create(value: string?)$ IdPattern
    +MatchStart(text: string) (string, string)?
  }
  class RequirementDocumentVersion {
    -id: Guid
    -number: int
    -fileName: string
    -contentType: string
    -sha256: string
    -uploadedById: Guid
    -uploadedAt: DateTime
    -added: int
    -changed: int
    -removed: int
    -skippedRows: List~string~
    +Summary: string
  }
  class IRequirementDocumentReaderFactory {
    <<interface>>
    +Supported: (string, DocumentKind)[]
    +For(extension: string, kind: DocumentKind) IRequirementDocumentReader?
  }
  class IRequirementDocumentReader {
    <<interface>>
    +Extension: string
    +Kind: DocumentKind
    +UsesIdPattern: bool
    +Read(content: byte[], idPattern: IdPattern) RequirementReadResult
  }
  class RequirementDocumentService {
    +UploadAsync(projectId: Guid, fileName: string, content: byte[], documentId: Guid?, idPattern: string?, kind: DocumentKind) Task~RequirementDocumentVersion~
  }
  class GitHubLink {
    <<owned by Project>>
    -repository: string
    -label: string
    -connectedAt: DateTime
    -connectedById: Guid
    -lastSyncedAt: DateTime?
    -lastSyncError: string?
    -skipped: List~SkippedIssue~
    -encryptedToken: string?
    -tokenChangedAt: DateTime?
    +HasToken: bool
    +SetToken(encryptedToken: string, at: DateTime)
    +RemoveToken(at: DateTime) bool
    +RecordSuccess(at: DateTime, skipped: SkippedIssue[])
    +RecordFailure(message: string)
  }
  class GitHubAddress {
    +Kind: GitHubAddressKind
    +Owner: string
    +Repository: string?
    +ProjectNumber: int?
    +Parse(address: string)$ GitHubAddress
  }
  class IGitHubProjects {
    <<interface>>
    +ListAsync(organization: string, token: string) Task
  }
  IGitHubProjects <|.. GitHubWorkItemSource

  class IWorkItemSource {
    <<interface>>
    +Source: WorkItemSource
    +ReadAsync(query: WorkItemQuery) Task~WorkItemReadResult~
  }
  class GitHubWorkItemSource
  class WorkItemSyncService {
    -projects: IProjectRepository
    -workItems: IWorkItemRepository
    -github: IWorkItemSource
    -notifier: IWorkItemNotifier
    -member: CurrentMember
    -log: IActivityLogger
    -secrets: ISecretProtector
    +LinkGitHubAsync(projectId: Guid, repository: string, label: string?) Task
    +SetGitHubTokenAsync(projectId: Guid, token: string) Task
    +RemoveGitHubTokenAsync(projectId: Guid) Task
    +SyncNowAsync(projectId: Guid) Task~SyncResult~
    +SyncAllAsync() Task
  }
  class ISecretProtector {
    <<interface>>
    +Protect(secret: string) string
    +Unprotect(protectedSecret: string) string
  }
  class DataProtectionSecretProtector
  class IWorkItemNotifier {
    <<interface>>
    +WorkItemsChangedAsync(projectId: Guid) Task
  }
  class EvidenceLink {
    -id: Guid
    -projectId: Guid
    -evidenceId: Guid
    -requirementId: Guid
    -source: LinkSource
    -linkedById: Guid
    -linkedRole: UserRole?
    -linkedAt: DateTime
    -removedById: Guid?
    -removedAt: DateTime?
    -needsReview: bool
    -carriedFromRequirementId: Guid?
    -reviewedById: Guid?
    -reviewedAt: DateTime?
    +Create(projectId, evidenceId, requirementId, linkedById, role, at, source)$ EvidenceLink
    +CarryTo(requirementId: Guid, textChanged: bool) EvidenceLink
    +MarkReviewed(userId: Guid, at: DateTime)
    +Remove(userId: Guid, at: DateTime)
  }
  class EvidenceLinkSuggestion {
    -id: Guid
    -projectId: Guid
    -evidenceId: Guid
    -requirementId: Guid
    -score: double
    -rank: int
    -rationale: string?
    -embeddingModel: string
    -promptVersion: string
    -status: SuggestionStatus
    +Approve(userId: Guid, at: DateTime, role: UserRole?)
    +Reject(reason: RejectionReason?, comment: string?, userId: Guid, at: DateTime, role: UserRole?)
  }
  class EvidenceSuggestion {
    -id: Guid
    -projectId: Guid
    -requirementId: Guid
    -source: EvidenceSourceKind
    -title: string
    -url: string?
    -externalId: string
    -passingCount: int
    -failingCount: int
    -status: EvidenceSuggestionStatus
    -evidenceId: Guid?
    +FromTestRun(...)$ EvidenceSuggestion
    +FromPullRequest(...)$ EvidenceSuggestion
    +Accept(evidenceId: Guid, userId: Guid, role: UserRole?, at: DateTime)
    +Reject(reason: string, userId: Guid, role: UserRole?, at: DateTime)
  }
  class EvidenceSuggestionService {
    +UploadTestResultsAsync(projectId: Guid, fileName: string, content: byte[]) Task~TestRunResult~
    +SuggestFromPullRequestsAsync(project: Project, token: string?) Task~int~
    +AcceptAsync(projectId: Guid, suggestionId: Guid) Task
    +RejectAsync(projectId: Guid, suggestionId: Guid, reason: string) Task
  }
  class ITestResultsReader {
    <<interface>>
    +Read(content: byte[]) TestCaseResult[]
  }
  class IPullRequestReader {
    <<interface>>
    +ClosingPullRequestsAsync(repository: string, issueNumber: int, token: string?) Task
  }
  EvidenceSuggestionService --> ITestResultsReader
  EvidenceSuggestionService --> IPullRequestReader
  EvidenceSuggestion ..> Evidence : becomes
  WorkItemSyncService --> EvidenceSuggestionService
  class EvidenceService {
    +AddAsync(projectId, title, url, fileName, content, validUntil) Task~EvidenceAdded~
    +LinkAsync(projectId: Guid, evidenceId: Guid, requirementIds: Guid[]) Task
    +UnlinkAsync(projectId: Guid, linkId: Guid) Task
    +RemoveAsync(projectId: Guid, evidenceId: Guid) Task
    +ShowMoreAsync(projectId: Guid, evidenceId: Guid) Task~int~
    +ApproveAsync(projectId: Guid, suggestionId: Guid) Task
    +RejectAsync(projectId: Guid, suggestionId: Guid, reason: RejectionReason?, comment: string?) Task
  }
  class EvidenceQueries {
    +ForProjectAsync(projectId: Guid) Task~EvidenceTab?~
  }
  class IEvidenceTextReader {
    <<interface>>
    +Read(contentType: string, content: byte[]) string?
  }
  Evidence "1" -- "*" EvidenceLink
  Evidence "1" -- "*" EvidenceLinkSuggestion
  EvidenceService --> IEvidenceTextReader
  EvidenceService --> IEmbeddingModel
  class RequirementLink {
    -id: Guid
    -projectId: Guid
    -requirementId: Guid
    -workItemId: Guid
    -source: LinkSource
    -linkedById: Guid
    -linkedAt: DateTime
    -removedById: Guid?
    -removedAt: DateTime?
    -needsReview: bool
    -carriedFromRequirementId: Guid?
    -reviewedById: Guid?
    -reviewedAt: DateTime?
    +IsActive: bool
    +Create(projectId: Guid, requirementId: Guid, workItemId: Guid, linkedById: Guid, at: DateTime)$ RequirementLink
    +CarryTo(requirementId: Guid, textChanged: bool) RequirementLink
    +MarkReviewed(userId: Guid, at: DateTime)
    +Remove(userId: Guid, at: DateTime)
  }
  class VersionCarryOver {
    +CarryAsync(project: Project, frameworkId: Guid, fromVersionId: Guid, toVersionId: Guid) Task~CarriedOver~
  }
  ProjectService --> VersionCarryOver
  VersionCarryOver ..> RequirementLink
  VersionCarryOver ..> RequirementApplicability
  VersionCarryOver ..> Evidence
  class ComplianceService {
    +LinkAsync(projectId: Guid, requirementId: Guid, workItemId: Guid) Task
    +UnlinkAsync(projectId: Guid, linkId: Guid) Task
    +MarkNotApplicableAsync(projectId: Guid, requirementId: Guid, reason: string) Task
    +MarkApplicableAsync(projectId: Guid, requirementId: Guid) Task
    +ConfirmReviewAsync(projectId: Guid, requirementId: Guid) Task
    +AddEvidenceLinkAsync(projectId: Guid, requirementId: Guid, title: string, url: string, validUntil: DateOnly?) Task
    +AddEvidenceFileAsync(projectId: Guid, requirementId: Guid, title: string, fileName: string, content: byte[], validUntil: DateOnly?) Task
    +RemoveEvidenceAsync(projectId: Guid, evidenceId: Guid) Task
  }
  class ComplianceQueries {
    +ForProjectAsync(projectId: Guid) Task~ProjectCompliance?~
    +EvidenceFileAsync(projectId: Guid, evidenceId: Guid) Task
  }
  class ApplicabilitySuggestion {
    -id: Guid
    -projectId: Guid
    -frameworkId: Guid
    -requirementId: Guid
    -reason: string
    -contextUsed: string
    -classifiedAt: DateTime?
    -model: string
    -promptVersion: string
    -createdAt: DateTime
    -status: SuggestionStatus
    -decidedById: Guid?
    -decidedRole: UserRole?
    -decidedAt: DateTime?
    +Create(projectId, frameworkId, requirementId, reason, contextUsed, classifiedAt, model, promptVersion, at)$ ApplicabilitySuggestion
    +Accept(userId: Guid, at: DateTime, role: UserRole?)
    +Reject(userId: Guid, at: DateTime, role: UserRole?)
    +Replace()
  }
  class AiContextBuilder {
    +ForAsync(project: Project, requirement: Requirement) Task~AiProjectContext~
  }
  class ApplicabilitySuggestionService {
    +SuggestAsync(projectId: Guid, frameworkId: Guid) Task~int~
    +AcceptAsync(projectId: Guid, suggestionId: Guid, reason: string?) Task
    +RejectAsync(projectId: Guid, suggestionId: Guid) Task
  }
  ApplicabilitySuggestionService --> AiContextBuilder
  ApplicabilitySuggestionService ..> ApplicabilitySuggestion
  RationaleService --> AiContextBuilder
  class MappingSuggestion {
    -id: Guid
    -projectId: Guid
    -workItemId: Guid
    -requirementId: Guid
    -score: double
    -rank: int
    -rationale: string?
    -embeddingModel: string
    -rationaleModel: string?
    -promptVersion: string
    -contextUsed: string?
    -createdAt: DateTime
    -status: SuggestionStatus
    -decidedById: Guid?
    -decidedAt: DateTime?
    -rejectionReason: RejectionReason?
    -comment: string?
    +Approve(userId: Guid, at: DateTime)
    +Reject(reason: RejectionReason?, comment: string?, userId: Guid, at: DateTime)
    +Replace()
    +SetRationale(rationale: string, model: string, contextUsed: string?)
  }
  class IAiLimits {
    <<interface>>
    +MinimumScoreForStories: double
    +MinimumScoreForEvidence: double
  }
  class ConfiguredAiLimits
  IAiLimits <|.. ConfiguredAiLimits
  MappingSuggestionService --> IAiLimits
  EvidenceService --> IAiLimits
  class IEmbeddingModel {
    <<interface>>
    +Name: string
    +EmbedAsync(texts: string[]) Task~float[][]~
  }
  class IRationaleWriter {
    <<interface>>
    +Name: string
    +WriteAsync(prompt: string) Task~string~
  }
  class MappingSuggestionService {
    +SuggestAsync(projectId: Guid) Task~int~
    +ShowMoreAsync(projectId: Guid, workItemId: Guid) Task~int~
    +ApproveAsync(projectId: Guid, suggestionId: Guid) Task
    +RejectAsync(projectId: Guid, suggestionId: Guid, reason: RejectionReason?, comment: string?) Task
  }
  class AiTask {
    <<enumeration>>
    Embedding
    Ranking
    Rationale
    EvidenceCheck
    GapDrafts
    RiskDrafts
    ImportFill
  }
  class IAiModels {
    <<interface>>
    +Embedding(task: AiTask) IEmbeddingModel
    +Writer(task: AiTask) IRationaleWriter
    +Overview() IReadOnlyList~AiTaskModel~
  }
  class ConfiguredAiModels {
    +Embedding(task: AiTask) IEmbeddingModel
    +Writer(task: AiTask) IRationaleWriter
    +Overview() IReadOnlyList~AiTaskModel~
  }
  ConfiguredAiModels ..|> IAiModels
  RationaleService --> IAiModels
  class AiEvaluation {
    -id: Guid
    -step: AiEvaluationStep
    -model: string
    -promptVersion: string
    -decisions: int
    -top1: double?
    -top3: double?
    -top10: double?
    -rejectedAboveLimit: double?
    -graded: int
    -averageGrade: double?
    -runBy: string
    -runAt: DateTime
    +ForMatching(...)$ AiEvaluation
    +ForRationales(model, promptVersion, grades, runBy, at)$ AiEvaluation
  }
  class AiEvaluationService {
    +EvaluateStoryMatchingAsync() Task~AiEvaluation~
    +RationaleSampleAsync() Task~IReadOnlyList~RationaleSample~~
    +GradeRationalesAsync(grades: IReadOnlyDictionary~Guid,int~) Task~AiEvaluation~
  }
  AiEvaluationService --> IAiModels
  AiEvaluationService ..> AiEvaluation
  class RationaleService {
    +WriteMissingAsync(take: int) Task~int~
  }
  class AiFeedbackService {
    +SetSharingAsync(share: bool) Task
    +ExportAsync() Task~string~
  }

  %% ================= Risici (US-042) =================
  class Scan {
    -id: Guid
    -projectId: Guid
    -tool: string
    -fileName: string
    -sha256: string
    -scannedAt: DateTime
    -uploadedById: Guid
    -new: int
    -fixed: int
    -open: int
    -openCritical: int
    +Create(projectId, tool, fileName, content, userId, at)$ Scan
    +Record(added: int, fixed: int, open: int, openCritical: int)
  }
  class Finding {
    -id: Guid
    -projectId: Guid
    -tool: string
    -fingerprint: string
    -ruleId: string
    -severity: FindingSeverity
    -message: string
    -location: string?
    -state: FindingState
    -fixedAt: DateTime?
    -acceptedReason: string?
    +SeenIn(scanId: Guid)
    +MissingFrom(scanId: Guid, at: DateTime) bool
    +Accept(reason: string, userId: Guid, at: DateTime)
  }
  class ScanSource {
    <<enumeration>>
    Upload
    GitHubCodeScanning
    GitHubDependabot
    CiUpload
  }
  class IGitHubSecurityAlerts {
    <<interface>>
    +ReadAsync(repository: string, token: string?) Task~IReadOnlyList~GitHubAlertRead~~
  }
  Scan --> ScanSource
  class IScanImporter {
    <<interface>>
    +CanRead(fileName: string, content: byte[]) bool
    +Read(content: byte[]) ScanReport
  }
  class SarifScanImporter
  SarifScanImporter ..|> IScanImporter
  Project "1" --> "0..*" Scan
  Project "1" --> "0..*" Finding
  class Risk {
    -id: Guid
    -version: uint
    -projectId: Guid
    -title: string
    -description: string
    -likelihood: int
    -impact: int
    -residualLikelihood: int?
    -residualImpact: int?
    -ownerId: Guid
    -treatment: RiskTreatment
    -status: RiskStatus
    -acceptedReason: string?
    -acceptedById: Guid?
    -closedReason: string?
    -lastReviewedAt: DateTime?
    -measures: List~RiskMeasure~
    -actions: List~RiskAction~
    +Score: int
    +Level: RiskLevel
    +Update(...) IReadOnlyList~ProjectChange~
    +Accept(reason: string, userId: Guid, at: DateTime) ProjectChange
    +Close(reason: string, at: DateTime) ProjectChange
    +MarkReviewed(userId: Guid, at: DateTime)
    +IsReviewDue(now: DateTime) bool
    +AddMeasure(requirementId: Guid?, workItemId: Guid?) RiskMeasure
    +AddAction(title: string, ownerId: Guid, dueOn: DateOnly?, userId: Guid, at: DateTime) RiskAction
    +LinkAction(actionId: Guid, workItemId: Guid?, evidenceId: Guid?) RiskActionLink
    +CompleteAction(actionId: Guid, hasProof: bool, userId: Guid, at: DateTime)
    +OverdueActions(today: DateOnly) int
  }
  class RiskTraces {
    <<static>>
    +For(risk: Risk, compliance: ProjectCompliance)$ RiskTrace
    +ByRequirement(risks: IEnumerable~Risk~, requirementsOfStory: ILookup~Guid,Guid~)$ IReadOnlyDictionary~Guid,IReadOnlyList~RiskReference~~
  }
  RiskTraces ..> Risk
  class RiskMeasure {
    -id: Guid
    -riskId: Guid
    -requirementId: Guid?
    -workItemId: Guid?
  }
  class RiskAction {
    -id: Guid
    -riskId: Guid
    -title: string
    -ownerId: Guid
    -dueOn: DateOnly?
    -status: RiskActionStatus
    -createdById: Guid
    -createdAt: DateTime
    -doneById: Guid?
    -doneAt: DateTime?
    -links: List~RiskActionLink~
    +IsOverdue(today: DateOnly) bool
  }
  class RiskActionLink {
    -id: Guid
    -actionId: Guid
    -workItemId: Guid?
    -evidenceId: Guid?
  }
  class RiskActionStatus {
    <<enumeration>>
    Planned
    InProgress
    Done
  }
  Risk "1" *-- "0..*" RiskAction
  RiskAction "1" *-- "0..*" RiskActionLink

  %% ================= Roller og invitationer (US-009, US-010) =================
  class Permission {
    <<enumeration>>
    ManageMembers
    ManageAiSharing
    ManageFrameworks
    DecideCompliance
    EditProjects
    LinkWork
    ManageRisks
  }
  class RolePermissions {
    <<static>>
    +Can(role: UserRole, permission: Permission)$ bool
  }
  class Invitation {
    -id: Guid
    -organizationId: Guid
    -role: UserRole
    -tokenHash: string
    -createdById: Guid
    -expiresAt: DateTime
    -acceptedById: Guid?
    -acceptedAt: DateTime?
    -revokedAt: DateTime?
    +EnsureUsable(now: DateTime)
    +MarkAccepted(userId: Guid, at: DateTime)
    +Revoke(at: DateTime)
  }
  class MemberService {
    +ChangeRoleAsync(memberId: Guid, role: UserRole) Task
    +CreateInvitationAsync(role: UserRole) Task~string~
    +RevokeInvitationAsync(invitationId: Guid) Task
    +AcceptInvitationAsync(token: string) Task
  }
  class SystemAdminService {
    +ListOrganizationsAsync() Task
    +ChangeRoleAsync(organizationId: Guid, memberId: Guid, role: UserRole) Task
  }

  %% ================= Reports (US-056) =================
  class TechnicalDocumentationQueries {
    +DraftAsync(projectId: Guid) Task~GeneratedDocument?~
    +CraOf(compliance: ProjectCompliance)$ FrameworkCompliance?
  }
  class GeneratedDocument {
    +FileName: string
    +Blocks: DocumentBlock[]
  }
  class DocumentBlock {
    +Kind: DocumentBlockKind
    +Text: string
  }
  class IDocumentWriter {
    <<interface>>
    +ToDocx(document: GeneratedDocument) byte[]
  }
  class OpenXmlDocumentWriter
  TechnicalDocumentationQueries --> ComplianceQueries
  TechnicalDocumentationQueries --> RiskQueries
  TechnicalDocumentationQueries ..> GeneratedDocument
  GeneratedDocument *-- DocumentBlock
  IDocumentWriter <|.. OpenXmlDocumentWriter
  %% ================= Feedback (US-063) =================
  class Feedback {
    -id: Guid
    -organizationId: Guid
    -userId: Guid
    -role: UserRole?
    -sentAt: DateTime
    -page: string
    -asA: string
    -iWant: string
    -soThat: string
    -rules: string?
    -examples: string?
    -screenshotFileName: string?
    -screenshotSha256: string?
    -status: FeedbackStatus
    -issueNumber: int?
    -issueUrl: string?
    -dismissReason: string?
    -decidedBy: string?
    -decidedAt: DateTime?
    +Send(...)$ Feedback
    +SuggestedIssueTitle() string
    +SuggestedIssueBody() string
    +IssueCreated(number: int, url: string, decidedBy: string, at: DateTime)
    +Dismiss(reason: string, decidedBy: string, at: DateTime)
  }
  class Screenshot {
    +FileName: string
    +ContentType: string
    +Content: byte[]
    +Sha256: string
    +Create(fileName: string, content: byte[])$ Screenshot
  }
  class FeedbackStatus {
    <<enumeration>>
    New
    IssueCreated
    Dismissed
  }
  class FeedbackService {
    +SendAsync(page, asA, iWant, soThat, rules, examples, screenshotName, screenshotContent) Task~Feedback~
    +CreateIssueAsync(feedbackId: Guid, title: string, body: string) Task~CreatedIssue~
    +DismissAsync(feedbackId: Guid, reason: string) Task
    +ListAsync() Task
  }
  class IFeedbackRepository {
    <<interface>>
  }
  class IFeedbackIssueWriter {
    <<interface>>
    +CreateAsync(title: string, body: string, labels: string[]) Task~CreatedIssue~
  }
  class GitHubFeedbackIssueWriter
  Feedback --> FeedbackStatus
  Feedback ..> Screenshot
  FeedbackService --> IFeedbackRepository
  FeedbackService --> IFeedbackIssueWriter
  IFeedbackIssueWriter <|.. GitHubFeedbackIssueWriter

  %% ================= Security events (US-073) =================
  class SecurityEvent {
    -id: Guid
    -projectId: Guid
    -kind: SecurityEventKind
    -title: string
    -awareAt: DateTime
    -fixAvailableAt: DateTime?
    -earlyWarningSentAt: DateTime?
    -notificationSentAt: DateTime?
    -finalReportSentAt: DateTime?
    -closedAt: DateTime?
    -closedReason: string?
    -disclosedAt: DateTime?
    -upgradedAt: DateTime?
    -upgradeReason: string?
    -firstAwareAt: DateTime?
    +IsReportable: bool
    +IsVulnerability: bool
    +Record(...)$ SecurityEvent
    +DueAt(report: SecurityReportKind) DateTime?
    +Status(report: SecurityReportKind, now: DateTime) SecurityReportStatus
    +MarkSent(report: SecurityReportKind, sentAt: DateTime, reference: string, now: DateTime)
    +MarkFixAvailable(at: DateTime, now: DateTime)
    +Close(userId: Guid, now: DateTime, reason: string?)
    +MarkDisclosed(at: DateTime, now: DateTime)
    +Upgrade(reportableAt: DateTime, reason: string, userId: Guid, now: DateTime)
  }
  class SecurityEventKind {
    <<enumeration>>
    ActivelyExploitedVulnerability
    SevereIncident
    VulnerabilityNotExploited
    IncidentNotSevere
  }
  class SecurityReportKind {
    <<enumeration>>
    EarlyWarning
    Notification
    FinalReport
  }
  class SecurityEventService {
    +RecordAsync(...) Task~SecurityEvent~
    +MarkSentAsync(...) Task
    +MarkFixAvailableAsync(...) Task
    +MarkDisclosedAsync(...) Task
    +UpgradeAsync(...) Task
    +CloseAsync(...) Task
  }
  class ISecurityEventRepository {
    <<interface>>
  }
  SecurityEvent --> SecurityEventKind
  SecurityEvent ..> SecurityReportKind
  SecurityEventService --> ISecurityEventRepository
  ProjectService --> ISecurityEventRepository : end of support

  %% ================= Log =================
  class ActivityGroup {
    <<enumeration>>
    SignIn
    Members
    Organisation
    Frameworks
    Projects
    WorkItems
    Requirements
    Evidence
    Risks
    Ai
  }
  class ActivityLogQuery {
    -search: string?
    -from: DateOnly?
    -to: DateOnly?
    -group: ActivityGroup?
    -userId: Guid?
    -systemOnly: bool
    -sort: ActivitySort
    -page: int
  }
  class ActivityLogEntry {
    -id: Guid
    -organizationId: Guid
    -userId: Guid?
    -action: ActivityAction
    -subjectId: Guid?
    -details: string?
    -occurredAt: DateTime
    -previousHash: string?
    -hash: string?
    +Seal(previousHash: string?) void
  }
  class ActivityLogChain {
    <<static>>
    +HashOf(entry: ActivityLogEntry, previousHash: string?) string
    +Verify(entriesInOrder: IEnumerable~ActivityLogEntry~) ChainCheck
  }
  class ChainCheck {
    +Checked: int
    +WithoutFingerprint: int
    +FirstBroken: ActivityLogEntry?
    +IsIntact: bool
  }
  ActivityLogChain ..> ActivityLogEntry
  ActivityLogChain ..> ChainCheck

  %% ================= Enums =================
  class Sector {
    <<enumeration>>
    Energy
    Transport
    Banking
    FinancialMarketInfrastructure
    Health
    DrinkingWater
    WasteWater
    DigitalInfrastructure
    IctServiceManagement
    PublicAdministration
    Space
    PostalAndCourier
    WasteManagement
    Chemicals
    Food
    Manufacturing
    DigitalProviders
    Research
    Other
  }
  class OrganizationSize {
    <<enumeration>>
    Micro
    Small
    Medium
    Large
  }
  class UserRole {
    <<enumeration>>
    Admin
    ComplianceManager
    Developer
    Auditor
  }
  class LifecyclePhase {
    <<enumeration>>
    Planning
    Development
    Released
    EndOfSupport
  }
  class ProjectClassification {
    -id: Guid
    -projectId: Guid
    -frameworkId: Guid
    -class: string
    -reason: string
    -classifiedById: Guid
    -classifiedRole: UserRole?
    -classifiedAt: DateTime
    +Create(...)$ ProjectClassification
  }
  class ClassificationService {
    +ClassifyAsync(projectId: Guid, frameworkId: Guid, classCode: string, reason: string) Task
    +ForProjectAsync(projectId: Guid) Task
  }
  class RegulationClasses {
    <<static>>
    +For(framework: Framework)$ ClassifiableRegulation?
  }
  Project "1" -- "*" ProjectClassification : history
  ClassificationService --> RegulationClasses
  class FrameworkScope {
    <<enumeration>>
    Organization
    Project
  }
  class CatalogSource {
    <<enumeration>>
    BuiltIn
    Imported
  }
  class FrameworkVersionStatus {
    <<enumeration>>
    Draft
    Active
  }
  class ActivityAction {
    <<enumeration>>
    OrganizationCreated
    UserSignedIn
    UserSignedOut
    CatalogImported
    CatalogActivated
    FrameworkEnabled
    FrameworkDisabled
    ProjectFrameworkAdded
    ProjectFrameworkRemoved
    ProjectFrameworkVersionChanged
    ProjectUpdated
    ProjectPhaseChanged
  }
  class MappingSource {
    <<enumeration>>
    Manual
    AiApproved
  }
  class ReleaseStatus {
    <<enumeration>>
    Planned
    InDevelopment
    Released
    EndOfSupport
  }
  class AssessmentStatus {
    <<enumeration>>
    NotAssessed
    Compliant
    PartiallyCompliant
    NonCompliant
    NotApplicable
  }
  class EvidenceType {
    <<enumeration>>
    Document
    ScanResult
    TestReport
    Sbom
    CodeChange
    ManualConfirmation
    Other
  }
  class WorkItemType {
    <<enumeration>>
    UserStory
    Requirement
    UseCase
    Task
    Bug
    Epic
  }
  class WorkItemSource {
    <<enumeration>>
    GitHub
    Document
  }
  class WorkItemState {
    <<enumeration>>
    Open
    Closed
    Removed
  }
  class SuggestionStatus {
    <<enumeration>>
    Pending
    Approved
    Rejected
    Superseded
  }
  class RejectionReason {
    <<enumeration>>
    NotRelevant
    WrongRequirement
    TooBroad
    AlreadyMapped
    InsufficientInformation
    Other
  }

  %% ================= Associationer =================
  Organization "1" --> "*" User : users
  Organization "1" --> "*" Project : projects
  Organization "1" --> "*" Control : controls
  Organization "1" --> "*" OrganizationFramework : frameworks
  Framework "*" --> "0..1" Organization : owner
  OrganizationFramework "*" --> "1" FrameworkVersion
  Project "1" --> "*" ProjectFramework : frameworks
  Project ..> ProjectChange : returns
  ProjectFramework "*" --> "1" FrameworkVersion
  Project "1" --> "*" Release : releases
  Project "1" --> "*" WorkItem : workItems
  Project "1" *-- "0..1" GitHubLink : gitHub
  GitHubWorkItemSource ..|> IWorkItemSource
  WorkItemSyncService --> IWorkItemSource : github
  Project "1" --> "*" RequirementDocument : requirement specifications
  RequirementDocument "1" *-- "1..*" RequirementDocumentVersion : versions
  WorkItem "*" --> "0..1" RequirementDocument : document
  RequirementDocumentService --> IRequirementDocumentReaderFactory : readers
  IRequirementDocumentReaderFactory ..> IRequirementDocumentReader : For(extension, kind)
  IRequirementDocumentReaderFactory <|.. RequirementDocumentReaderFactory
  IRequirementDocumentReader <|.. ExcelRequirementReader
  IRequirementDocumentReader <|.. CsvRequirementReader
  IRequirementDocumentReader <|.. WordRequirementReader
  IRequirementDocumentReader <|.. UseCaseReader
  WorkItem "1" *-- "0..1" UseCaseSections : useCase
  WorkItemSyncService --> IWorkItemNotifier : notifier
  WorkItemSyncService --> ISecretProtector : secrets
  DataProtectionSecretProtector ..|> ISecretProtector
  WorkItem "*" --> "0..1" Release
  Framework "1" --> "*" FrameworkVersion : versions
  FrameworkVersion "1" --> "*" Requirement : requirements
  Control "1" --> "*" RequirementControl : requirements
  RequirementControl "*" --> "1" Requirement
  Control "*" --> "1" User : owner
  Evidence "*" --> "1" Project
  Evidence "*" --> "1" Requirement
  Evidence ..> EvidenceFile : made from
  RequirementLink "*" --> "1" WorkItem
  RequirementLink "*" --> "1" Requirement
  RequirementLink "*" --> "1" Project
  RequirementApplicability "*" --> "1" Requirement
  Organization "1" --> "*" Invitation : invitations
  Project "1" --> "*" Risk : risks
  Risk "1" *-- "*" RiskMeasure : measures
  RiskMeasure "*" --> "0..1" Requirement
  RiskMeasure "*" --> "0..1" WorkItem
  MappingSuggestion "*" --> "1" WorkItem
  MappingSuggestion "*" --> "1" Requirement
  MappingSuggestionService --> IEmbeddingModel : step 1
  RationaleService --> IRationaleWriter : step 2
  OllamaEmbeddingModel ..|> IEmbeddingModel
  OpenAiCompatibleEmbeddingModel ..|> IEmbeddingModel
  OllamaRationaleWriter ..|> IRationaleWriter
  OpenAiCompatibleRationaleWriter ..|> IRationaleWriter
  RequirementApplicability "*" --> "1" Project
  RequirementAssessment "*" --> "1" Requirement
  RequirementAssessment "*" --> "0..1" Release
  RequirementAssessment "*" --> "0..1" Organization
  MappingSuggestion "*" --> "1" Requirement
  MappingSuggestion "*" --> "0..1" WorkItem
  MappingSuggestion "*" --> "0..1" Control
  ActivityLogEntry "*" --> "1" Organization
  ActivityLogEntry "*" --> "1" User
  CurrentMember --> ICurrentUser : currentUser
  CurrentMember --> IOrganizationRepository : organizations
  ActivityLogger ..|> IActivityLogger
  ActivityLogger --> IActivityLogRepository : log
  ActivityLogger --> CurrentMember : member
  OrganizationService --> IOrganizationRepository : organizations
  OrganizationService --> ICurrentUser : currentUser
  OrganizationService --> CurrentMember : member
  OrganizationService --> IActivityLogger : log
  OrganizationService --> IUnitOfWork : unitOfWork
  UserActivityService --> IOrganizationRepository : organizations
  UserActivityService --> CurrentMember : member
  UserActivityService --> IActivityLogger : log
  UserActivityService --> IUnitOfWork : unitOfWork
  FrameworkService --> IFrameworkRepository : frameworks
  FrameworkService --> IRequirementExtractor : extractor
  PdfRequirementExtractor ..|> IRequirementExtractor
  FrameworkService --> CurrentMember : member
  FrameworkService --> IActivityLogger : log
  FrameworkService --> IUnitOfWork : unitOfWork
  OrganizationQueries --> CurrentMember : member
  FrameworkQueries --> IFrameworkRepository : frameworks
  FrameworkQueries --> CurrentMember : member
  ProjectQueries --> IProjectRepository : projects
  ProjectQueries --> CurrentMember : member
  FrameworkService ..> CatalogFile : validerer
  ProjectService --> IProjectRepository : projects
  ProjectService --> CurrentMember : member
  ProjectService --> IActivityLogger : log
  ProjectService --> IUnitOfWork : unitOfWork
  ProjectService ..> Project : opretter
  IProjectRepository ..> Project
  ProjectService --> IFrameworkRepository : frameworks
  FrameworkService --> IProjectRepository : projects
  IFrameworkRepository ..> Framework
  IFrameworkRepository ..> OrganizationFramework
  Framework ..> CatalogFile
  OrganizationService ..> Organization : opretter
  IOrganizationRepository ..> Organization
  IActivityLogRepository ..> ActivityLogEntry

  note for MappingSuggestion "{xor} workItemId eller controlId"
  note for RequirementAssessment "{xor} releaseId eller organizationId"
  note for FrameworkVersion "BuiltIn: ownerOrganizationId = null / Imported: ownerOrganizationId påkrævet"
```

## Designvalg

- **User stories læses fra teamets eget værktøj.** `IWorkItemSource` er adapteren (B11); `GitHubWorkItemSource` læser issues med projektets label via GitHubs REST API uden nøgle og forstår BDD-skabelonen (`### Rules`, `### Examples`, `### Gherkin`). `WorkItemSyncService` tilføjer, opdaterer og markerer (`Closed`, `Removed`) og sletter aldrig. "Sync now" og en baggrundsjob (`ScheduledWorkItemSync`, hvert 15. minut) kører den samme sync, og åbne projektsider får besked via SignalR (`IWorkItemNotifier`). En planlagt sync logges uden bruger (`userId` er null = systemet) (B25). (US-018)
- **Private repositories læses med en krypteret nøgle.** Domænet ser kun den krypterede tekst (`GitHubLink.EncryptedToken`). `ISecretProtector` krypterer med ASP.NET Core Data Protection, og nøglerne ligger i tabellen `DataProtectionKeys`. Nøglen tjekkes hos GitHub, før den gemmes, og den vises, logges og sendes aldrig tilbage til siden (B26). (US-031)
- **Projektsiden har faner.** Overview, Frameworks, Product requirements (indtil US-104 "User stories") og Activity har hver sin adresse (`/Projects/Details/{id}/{tab}`) og samme header. Activity viser projektets logposter (alle med projektet som `subjectId`), nyeste først, med `details`. En handling, der fejler, sender brugeren tilbage til sin egen fane (`[OnDomainError(Tab = ...)]`). (US-022)
- **En version kan gælde fra en dato.** `AppliesFrom` sættes på kladden og låses ved aktivering. `CurrentVersion(today)` er den aktive version, der senest begyndte at gælde. Har ingen version startet endnu, bruges den første kommende. "I dag" kommer fra appens ur (`TimeProvider.Today()`), så testene kan styre datoen. Projekter flyttes ikke automatisk, men kan forberede sig ved at vælge en kommende version (B27). (US-035)
- **Krav og user stories kobles pr. projekt.** `RequirementLink` er sit eget aggregat og laves i hånden indtil videre. Senere skal godkendte AI-forslag lave samme slags kobling (US-020). En kobling slettes aldrig, den får `removedAt`. Kun aktive koblinger tæller som dækning. Compliance-fanen har én under-fane pr. framework (`/Projects/Details/{id}/compliance/{slug}`) med krav, koblede stories og dækning. (US-019, US-021, US-041)
- **CRA's krav følger med trackeren.** Bilag I (del I og II, 22 krav) er seed-data på den indbyggede CRA-version 1, med faste id'er, den officielle tekst fra EUR-Lex, en `group` pr. del og en `reference` til bilaget. Et krav kan markeres "Not applicable" for et projekt med en begrundelse (`RequirementApplicability`) og tæller så ikke med i dækningen. (US-043)
- **AI-forslag i to trin, og modellen kan skiftes.** Trin 1: en embedding-model (`IEmbeddingModel`) scorer hvert krav mod hver story, og de 4 bedste bliver `MappingSuggestion`. Trin 2: en lille chat-model (`IRationaleWriter`) skriver begrundelsen i baggrunden (`RationaleWorker`). Hvilken server og hvilke modeller der bruges, står i `Ai:*`, og der er adaptere til Ollama og til OpenAI-kompatible servere. Hvert forslag gemmer model og prompt-version, og en godkendelse bliver til et `RequirementLink` med kilden `AiApproved` (B29). (US-020, US-036)
- **Hvert projekt har et risikoregister.** `Risk` har sandsynlighed og konsekvens fra 1 til 5. Niveauet beregnes: 1–4 Low, 5–9 Medium, 10–16 High, 20–25 Critical. En risiko har en ejer og en behandling og kan få en valgfri restrisiko. Den kobles til de krav og user stories, der mindsker den (`RiskMeasure`). En risiko slettes aldrig, den lukkes med en begrundelse. Efter 12 måneder uden review vises "Review due" (B30). (US-042)
- **Roller styrer, hvad et medlem må, og en beslutning husker rollen.** `RolePermissions` er rettighedstabellen, og hvert brugsscenarie spørger `CurrentMember.RequireAsync(Permission)`. Logposter, AI-beslutninger, koblinger, "not applicable", accepterede risici og aktiverede versioner gemmer rollen på beslutningstidspunktet, så en senere op- eller nedgradering ikke ændrer fortiden. System-admin er en Keycloak-rolle (`ICurrentUser.IsSystemAdmin`) og har kun adgang til organisationer og medlemmer (B31). (US-009, US-010)
- **Evidens hører til kravet i et projekt i MVP'en.** `Evidence` er sit eget aggregat ligesom `RequirementLink`. Det er et link eller en fil (højst 10 MB, PDF/PNG/JPG/TXT/CSV/MD) med SHA-256 og eventuelt en "gyldig til"-dato. Filens indhold ligger i sin egen tabel (`EvidenceDocuments`), så listen aldrig henter filerne. Evidens slettes aldrig, den får `removedAt`. Koblingen til `Control` og `Release` kommer, når de bygges (B33). (US-057)
- **Koblingsklasser med data.** `RequirementControl` og `RequirementLink` registrerer via `MappingSource`, om koblingen er lavet manuelt eller via et godkendt AI-forslag.
- **Vurderinger ændres ikke.** En ny `RequirementAssessment` oprettes i stedet, og den seneste er gældende. Det giver historik til en auditor.
- **Frameworkversioner låses ved aktivering.** Når `status` er `Active`, kan `requirements` ikke ændres. Så kan en mapping nøjes med at pege på et `Requirement`, fordi teksten bag aldrig ændrer sig. En rettelse er en ny `FrameworkVersion`.
- **En ny upload er en kladde.** `IRequirementExtractor` (PdfPig, ingen AI) finder linjer, der starter med en nummereret kode som `A.5.15`, og lægger dem i kladden. Medlemmet retter, sletter og tilføjer krav og aktiverer så versionen (mindst ét krav). `CurrentVersion` er den senest aktiverede version og er `null`, mens frameworket kun har en kladde; `LatestVersion` er den nyeste upload. Projekter kan kun bruge aktive versioner, og en upload flytter ingen projekter (B24). (US-007)
- **`OrganizationFramework` og `ProjectFramework` peger på en version**, ikke et framework. Klassenavnene er beholdt, fordi de stadig beskriver "hvilket framework er valgt"; versionen er detaljen.
- **`Framework` indeholder ingen kravtekst.** Den er kun navn og niveau, så den kan seedes som fælles referencedata uden ophavsretlige problemer.
- **`ActivityLogEntry` er append-only.** Ingen opdatering eller sletning, heller ikke for Admin.
- **Enums gemmes som tekst** i databasen (`.HasConversion<string>()`), så nye værdier ikke forskyder eksisterende data.
- **`SourceSystem` indeholder kun det, der virker.** Azure DevOps, Jira og Internal tilføjes, når adapterne findes.
- **Integration** (`IWorkItemSource` og adaptere) tilføjes, når den første story om GitHub-integration tages ind.
- **`Organization.Create` er en statisk fabriksmetode** i stedet for en offentlig constructor, så en organisation ikke kan oprettes, uden at reglerne er tjekket. (US-001)
- **Validering deles i to.** Regler for de enkelte felter (navn, CVR-format, sektor) ligger som guard clauses i `Organization.Create`. Regler, der kræver kendskab til andre organisationer (unikt CVR), ligger i `OrganizationService`. (US-001)
- **Repositories er interfaces i Application-laget.** De beskriver kun, hvad der skal kunne gemmes og slås op. Implementeringen ligger i Infrastructure (EF Core og PostgreSQL), og testene har en in-memory-udgave. (US-001, US-002)
- **`IActivityLogRepository` har kun `AppendAsync`.** Der findes ingen metode til at ændre eller slette logposter, så append-only håndhæves af selve interfacet. (US-001)
- **Repositories gemmer ikke selv.** De registrerer kun ændringer. `IUnitOfWork.SaveChangesAsync` gemmer det hele i én transaktion, så en organisation og logposten for dens oprettelse enten gemmes sammen eller slet ikke. (US-002)
- **Databasens nøgler holdes ude af domænet.** `User.organizationId` findes kun som fremmednøgle i databasen, og `ActivityLogEntry` får et fortløbende id, som kun databasen kender (shadow properties). Domæneklasserne er derfor uændrede. (US-002)
- **Connection string ligger aldrig i repoet.** Den læses fra miljøvariablen `ConnectionStrings__ComplianceTracker`. Integrationstestene starter deres egen PostgreSQL-container med Testcontainers. (US-002)
- **Web-UI'et er en tynd skal.** `OrganizationsController` (ASP.NET Core MVC) oversætter kun mellem formularen og `OrganizationService`. Fejlbeskeder kommer fra `DomainException`, så reglerne kun findes ét sted. Web-laget er ikke med i diagrammet, fordi det ikke er en del af modellen. (US-003)
- **Formularer kræver antiforgery-token** (`[ValidateAntiForgeryToken]`), så kun formularer fra applikationen selv kan oprette data (ASVS V3.5.1). (US-003)
- **Brugere genkendes på `externalId`**, Keycloaks `sub`-claim, og aldrig på navn eller e-mail, som kan ændres eller deles (ASVS V10.5.2). Kolonnen har et unikt index. (US-004)
- **`UserIdentity` er den indloggede persons identitet**, som Web-laget laver ud fra login-cookien og giver videre til Application og Domain. Domænet ved ikke, at den kommer fra Keycloak. (US-004)
- **Én organisation pr. bruger.** `OrganizationService` afviser en anden organisation for samme bruger, og en bruger kan kun se sin egen (andre giver 404). (US-004)
- **Log ind og log ud logges af `UserActivityService`.** Første login logges ikke, fordi brugeren endnu ikke har en organisation; oprettelsen logges i stedet som `OrganizationCreated`. (US-004)
- **Frameworks uploades som PDF.** En version er et dokument med versionsnummer, filnavn, størrelse og SHA-256-fingeraftryk. Kravene trækkes først ud af PDF'en i US-007; indtil da er `requirements`, `status` og `activatedAt` ikke i brug. (US-005)
- **`CatalogFile` er et value object**, som kun kan oprettes med en gyldig PDF på højst 20 MB og et renset filnavn. Reglerne for filer findes derfor ét sted (ASVS V5.2.1, V5.2.2, V5.3.2). (US-005)
- **Et framework har altid mindst én version**, og versionsnumrene tælles af `Framework` selv. En version ændres aldrig; en ny upload er en ny version, og `CurrentVersion` er den nyeste. (US-005)
- **PDF'en gemmes i sin egen tabel** (`CatalogDocuments` i Infrastructure), så lister over frameworks ikke henter filindholdet. Domænet kender kun fingeraftrykket. (US-005)
- **Alle opslag filtrerer på organisationen.** `IFrameworkRepository` tager altid `organizationId`, så en bruger aldrig kan hente en anden organisations filer, heller ikke ved at gætte et id (ASVS V8.2.2). (US-005)
- **Logposter kan pege på det, de handler om** via `subjectId`, fx frameworkversionen ved `CatalogImported`. (US-005)
- **Et projekt kræver navn og tilsigtet formål.** Det tilsigtede formål afgør, hvilke krav der gælder (CRA Annex VII, punkt 1(a)). Beskrivelse og scope er valgfri, og fasen starter som `Planning`. Navnet er unikt i organisationen uden hensyn til store og små bogstaver; det tjekkes i `ProjectService`. (US-008)
- **`LifecyclePhase` dækker hele produktets levetid**: `Planning`, `Development`, `Released` og `EndOfSupport`. De erstatter de tidligere foreslåede `Design`, `Maintenance` og `Retired` (B19). (US-008)
- **Indbyggede frameworks har ingen ejer.** CRA og AI Act seedes af en migration med faste id'er, `ownerOrganizationId = null`, én version uden fil og uploader og et link til den officielle tekst på EUR-Lex i `sourceReference`. Kravene seedes ikke endnu (B20). (US-006)
- **Indbyggede frameworks kan ikke ændres.** `Framework.AddVersion` afviser dem (`IsBuiltIn`), og `IFrameworkRepository.GetAsync` finder kun organisationens egne frameworks, så upload af en ny version giver en fejl. (US-006)
- **At slå et framework til er en `OrganizationFramework`-række.** Slås det fra, sættes `disabledAt`; rækken slettes aldrig, så historikken bevares, og slås det til igen, oprettes en ny række. Begge dele logges som `FrameworkEnabled`/`FrameworkDisabled` med versionen som `subjectId`. `ListAsync` viser organisationens egne frameworks og de indbyggede, der er slået til. (US-006)
- **Et projekt følger én version af hvert framework.** `Project` holder sine `ProjectFramework`-rækker og afviser en anden version af et framework, det allerede bruger. Databasen har samme regel som et unikt index på projekt og framework for aktive rækker. `frameworkId` gemmes ved siden af versionen, så reglen er nem at tjekke. (US-011)
- **Versionsskift og fjernelse bevarer historikken.** Et versionsskift sætter `removedAt` på den gamle række og opretter en ny; en fjernelse sætter kun `removedAt`. Rækker slettes aldrig. (US-011)
- **Projektet vælger blandt organisationens frameworks**: de uploadede og de indbyggede, der er slået til (`IFrameworkRepository.GetAvailableAsync`). Et indbygget framework kan ikke slås fra, mens projekter bruger det. (US-011)
- **En ny version flytter kun de valgte projekter.** `FrameworkService.UploadVersionAsync` tager de projekter, brugeren har krydset af, og gemmer versionen og versionsskiftene i én transaktion. De andre projekter beholder deres version. (US-011)
- **Logposter om projektets frameworks peger på projektet** (`subjectId`). Framework og versioner står i `ProjectFramework`-rækkerne med samme tidspunkt og bruger. (US-011)
- **Services læser tiden fra `TimeProvider`**, ikke fra `DateTime.UtcNow`. Appen bruger systemets ur; testene sætter et fast tidspunkt, så de kan tjekke, at hver logpost har præcis det tidspunkt, handlingen skete (B22). (US-016)
- **Størrelse er påkrævet ligesom sektor.** `Organization.Create` tager `OrganizationSize?` og afviser `null` med "Size is required"; formularen starter med "-- Choose size --". (US-016)
- **Den indloggede bruger findes ét sted.** `ICurrentUser` er personen bag forespørgslen (Web læser den fra login-cookien). `CurrentMember` slår brugerens organisation op én gang pr. forespørgsel. Use cases og queries får dem via dependency injection og tager ikke længere brugeren som parameter, så ingen use case kan handle for en anden bruger ved en fejl. (US-029)
- **Use cases ændrer, queries læser.** `...Service` ændrer data; `OrganizationQueries`, `FrameworkQueries` og `ProjectQueries` læser data til siderne og filtrerer altid på brugerens organisation. Controllere afhænger kun af disse og aldrig af repositories (testet i `ApplicationStructure.feature`). (US-029)
- **Logposter skrives kun af `IActivityLogger`**, der udfylder bruger, organisation og tidspunkt. Domain events er fravalgt indtil videre (B23). (US-029)
- **Hvert lag registrerer sig selv** med `AddApplication()` og `AddInfrastructure()`. Containeren tjekker ved opstart, at alle services og controllere kan oprettes. Keycloak-indstillingerne er en `KeycloakOptions`-klasse, der tjekkes ved opstart. (US-029)
- **Ét fælles filter viser brudte regler.** `DomainExceptionFilter` viser en formular igen med det indtastede (`IFormModel`) eller sender brugeren tilbage til siden med fejlen (`[OnDomainError]`, Post/Redirect/Get via TempData). Controllerne har ingen `try/catch`. (US-029)
- **Et projekt kan rettes og skifte fase.** `Update` og `ChangePhase` har samme regler som `Create` og returnerer, hvad der ændrede sig (`ProjectChange`). Logposten gemmer gammel og ny værdi i `Details`, fx "Phase: Planning → Development". Faserne kan skiftes frit, også baglæns, men EndOfSupport er endelig: projektet kan så hverken rettes, skifte fase eller ændre frameworks (`IsClosed`). (US-024)
- **Projekter følger samme adgangsregel som frameworks**: `IProjectRepository` tager altid `organizationId`, og andre organisationers projekter giver 404. Oprettelse logges som `ProjectCreated` med projektet som `subjectId`. (US-008)

## Ordliste

| Domænemodel (dansk) | Kode (engelsk) |
|---|---|
| Organisation | Organization |
| Bruger | User |
| Projekt | Project |
| Release | Release |
| Framework | Framework |
| Frameworkversion | FrameworkVersion |
| Krav | Requirement |
| Kontrol | Control |
| Kravvurdering | RequirementAssessment |
| Evidens | Evidence |
| Arbejdselement | WorkItem |
| Mappingforslag | MappingSuggestion |
| Logpost | ActivityLogEntry |
| Tilsigtet formål | IntendedPurpose |
| Kildesystem | SourceSystem |
| Indbygget / importeret | BuiltIn / Imported |
