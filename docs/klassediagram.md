# Compliance-tracker – Klassediagram

Senest opdateret: 2026-10-19

Klassediagrammet bygger på [domænemodellen](domaenemodel.md) og viser modellagets klasser med attributter, datatyper og associationer. **Metoder tilføjes løbende**, når en user story kræver dem, og opdateres i samme pull request som koden. Reglerne, som metoderne skal håndhæve, står i domænemodellen.

Koden er på engelsk; se ordlisten nederst.

## Notation

| Tegn | Betydning | I C# |
|---|---|---|
| `-` | privat | `private` felt (eller property med `private set`) |
| `+` | offentlig | `public` |
| `Type?` | kan være tom | nullable type |

## Diagram

```mermaid
classDiagram
  direction TB

  %% ================= Organisation og brugere =================
  class Organization {
    -id: Guid
    -name: string
    -cvr: string
    -sector: Sector
    -size: OrganizationSize
    -users: List~User~
    -projects: List~Project~
    -controls: List~Control~
    -frameworks: List~OrganizationFramework~
  }
  class User {
    -id: Guid
    -organizationId: Guid
    -name: string
    -email: string
    -role: UserRole
  }

  %% ================= Projekt og releases =================
  class Project {
    -id: Guid
    -organizationId: Guid
    -name: string
    -description: string
    -intendedPurpose: string
    -scope: string
    -phase: LifecyclePhase
    -craClass: CraClass
    -releases: List~Release~
    -workItems: List~WorkItem~
    -frameworks: List~ProjectFramework~
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
    -name: string
    -version: string
    -scope: FrameworkScope
    -requirements: List~Requirement~
  }
  class Requirement {
    -id: Guid
    -frameworkId: Guid
    -code: string
    -category: string
    -title: string
    -description: string
  }
  class OrganizationFramework {
    -organizationId: Guid
    -frameworkId: Guid
    -selectedAt: DateTime
  }
  class ProjectFramework {
    -projectId: Guid
    -frameworkId: Guid
    -selectedAt: DateTime
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
    -controlId: Guid
    -releaseId: Guid?
    -submittedById: Guid
    -type: EvidenceType
    -description: string
    -reference: string
    -validFrom: DateOnly
    -validTo: DateOnly?
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
    -title: string
    -acceptanceCriteria: string
    -sourceSystem: SourceSystem
    -externalReference: string
    -lastSyncedAt: DateTime
    -requirements: List~RequirementWorkItem~
  }
  class RequirementWorkItem {
    -requirementId: Guid
    -workItemId: Guid
    -source: MappingSource
    -createdAt: DateTime
  }
  class MappingSuggestion {
    -id: Guid
    -requirementId: Guid
    -workItemId: Guid?
    -controlId: Guid?
    -score: double
    -aiRationale: string
    -status: SuggestionStatus
    -rejectionReason: RejectionReason?
    -userComment: string?
    -createdAt: DateTime
    -decidedById: Guid?
    -decidedAt: DateTime?
  }

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
    Design
    Development
    Released
    Maintenance
    Retired
  }
  class CraClass {
    <<enumeration>>
    Default
    ImportantClassI
    ImportantClassII
    Critical
    NotApplicable
  }
  class FrameworkScope {
    <<enumeration>>
    Organization
    Project
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
    UseCase
    Task
    Bug
    Epic
  }
  class SourceSystem {
    <<enumeration>>
    Manual
    GitHub
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
  OrganizationFramework "*" --> "1" Framework
  Project "1" --> "*" ProjectFramework : frameworks
  ProjectFramework "*" --> "1" Framework
  Project "1" --> "*" Release : releases
  Project "1" --> "*" WorkItem : workItems
  WorkItem "*" --> "0..1" Release
  Framework "1" --> "*" Requirement : requirements
  Control "1" --> "*" RequirementControl : requirements
  RequirementControl "*" --> "1" Requirement
  Control "*" --> "1" User : owner
  Control "1" --> "*" Evidence : evidence
  Evidence "*" --> "0..1" Release
  WorkItem "1" --> "*" RequirementWorkItem : requirements
  RequirementWorkItem "*" --> "1" Requirement
  RequirementAssessment "*" --> "1" Requirement
  RequirementAssessment "*" --> "0..1" Release
  RequirementAssessment "*" --> "0..1" Organization
  MappingSuggestion "*" --> "1" Requirement
  MappingSuggestion "*" --> "0..1" WorkItem
  MappingSuggestion "*" --> "0..1" Control

  note for MappingSuggestion "{xor} workItemId eller controlId"
  note for RequirementAssessment "{xor} releaseId eller organizationId"
```

## Designvalg

- **Koblingsklasser med data.** `RequirementControl` og `RequirementWorkItem` registrerer via `MappingSource`, om koblingen er lavet manuelt eller via et godkendt AI-forslag.
- **Vurderinger ændres ikke.** En ny `RequirementAssessment` oprettes i stedet, og den seneste er gældende. Det giver historik til en auditor.
- **Enums gemmes som tekst** i databasen (`.HasConversion<string>()`), så nye værdier ikke forskyder eksisterende data.
- **`SourceSystem` indeholder kun det, der virker.** Azure DevOps, Jira og Internal tilføjes, når adapterne findes.
- **Integration** (`IWorkItemSource` og adaptere) tilføjes, når den første story om GitHub-integration tages ind.

## Ordliste

| Domænemodel (dansk) | Kode (engelsk) |
|---|---|
| Organisation | Organization |
| Bruger | User |
| Projekt | Project |
| Release | Release |
| Framework | Framework |
| Krav | Requirement |
| Kontrol | Control |
| Kravvurdering | RequirementAssessment |
| Evidens | Evidence |
| Arbejdselement | WorkItem |
| Mappingforslag | MappingSuggestion |
| Tilsigtet formål | IntendedPurpose |
| Kildesystem | SourceSystem |
