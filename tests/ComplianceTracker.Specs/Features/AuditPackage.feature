Feature: Audit package for an external auditor

  # US-076: one locked ZIP with everything the auditor asks for: a contents page, the reports, every evidence file with
  # its SHA-256, the activity log for the period, the requirement documents, the SBOMs and the audits. It is kept like a
  # locked report, so it never changes and its fingerprint can be checked.

  Background:
    Given an empty database
    And the time is "2026-11-12T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And I have added the evidence "MFA config" with the file "mfa.pdf" to "I.2(d)"

  @US-076 @iso27001-9.2 @integration
  Scenario: The package holds the reports and the evidence
    Given I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    And an audit of "Acme Portal" found "Access reviews not documented" for "Anna" due "2026-12-31"
    When I create the audit package of "Acme Portal"
    Then the audit package contains "00-contents.txt", "01-traceability-cra.pdf", "02-compliance-status.pdf" and "04-risk-assessment.pdf"
    And the audit package contains "evidence/MFA-config-mfa.pdf"
    And the audit package contains "sbom/2.1.0-sbom.cdx.json"
    And the audit package contains "05-activity-log-2025-11-12-to-2026-11-12.csv"
    And the audit package's contents give "evidence/MFA-config-mfa.pdf" the SHA-256 of the evidence "MFA config"
    And the audit package's contents say "Finding: Access reviews not documented, due 31-12-2026, open"

  @US-076 @iso27001-5.35 @integration
  Scenario: A part that cannot be made is listed as not included
    When I create the audit package of "Acme Portal"
    Then the audit package's contents say "Statement of Applicability (US-053): The organisation has no ISO/IEC 27001 framework"

  @US-076 @iso27001-A.5.33 @integration
  Scenario: The package is locked with its fingerprint and logged
    When I create the audit package of "Acme Portal"
    Then the locked reports of "Acme Portal" list "Audit package – Acme Portal, 12-11-2025 – 12-11-2026" locked by "Anna (Organisation admin)" with a fingerprint
    And the activity log has "AuditPackageCreated" with the package's fingerprint

  @US-076 @integration
  Scenario: The period can be chosen
    When I create the audit package of "Acme Portal" from "2026-01-01" to "2026-06-30"
    Then the audit package contains "05-activity-log-2026-01-01-to-2026-06-30.csv"

  @US-076 @iso27001-A.5.15 @integration
  Scenario: An auditor can make a package
    Given "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" creates the audit package of "Acme Portal"
    Then the project "Acme Portal" has 1 audit package
