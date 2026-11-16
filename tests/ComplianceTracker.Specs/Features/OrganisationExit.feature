Feature: Export all of the organisation's data and close the organisation

  # US-184: the organisation admin downloads all the organisation's data in open formats, and can close the organisation:
  # after 30 days (undoable until then) everything is deleted, the organisation and its members are anonymised, and the
  # activity log is kept. Another organisation is never touched.

  Background:
    Given an empty database
    And the time is "2026-11-16T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And I have added the evidence "MFA config" with the file "mfa.pdf" to "I.2(d)"
    And the risk "Phishing" exists in "Acme Portal"
    And I have added the action "Phishing training" to the risk "Phishing"
    And "Acme Portal" has a "Trivy" scan with the findings "CVE-2026-1111" and "CVE-2026-2222"
    And I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    And an audit of "Acme Portal" found "Access reviews not documented" for "Anna" due "2026-12-31"
    And I have uploaded the policy "Access control policy" version "v1" with the content "Policy text v1"
    And I link the document "Access control policy" to "I.2(d)"
    And I have locked the traceability report of "Acme Portal" for "team"
    And I have added the supplier "Hosting ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    And "Hosting ApS" has the evidence "ISO/IEC 27001 certificate" valid until "2027-10-01"
    And I have added the checklist item "Architecture review held" to the phase "Design" with the reference ""
    And the framework "Security policy" has a draft with "A.5.15 Access control"
    And "Carl" has joined "Acme ApS" as "Developer"
    And "Bo" has created the organisation "Beta ApS" with the project "Beta Shop"

  @US-184 @gdpr-20 @integration
  Scenario: The export holds the organisation's data and files
    When I export everything of "Acme ApS"
    Then the export contains "00-contents.txt"
    And the export's file "data/Projects.json" says "Acme Portal"
    And the export's file "data/Projects.json" does not say "Beta Shop"
    And the export has a file under "files/EvidenceDocuments/"
    And the activity log has "OrganisationExported" saying "SHA-256"

  @US-184 @gdpr-17 @iso27001-A.8.10 @integration
  Scenario: A closed organisation is deleted after the waiting period, and another one is untouched
    Given I have started closing "Acme ApS"
    When the time is "2026-12-16T10:00:00Z"
    And the closing of organisations runs
    Then nothing of "Acme ApS" is left but its activity log and anonymous members
    And the organisation "Acme ApS" is now called "Closed organisation"
    And "Beta ApS" still has the project "Beta Shop"
    And the built-in "CRA – Regulation (EU) 2024/2847" still has its requirements
    And the activity log has "OrganisationClosed" saying "All data deleted"

  @US-184 @integration
  Scenario: Closing can be undone within the waiting period
    Given I have started closing "Acme ApS"
    And every member sees that "Acme ApS" is deleted on "16-12-2026"
    When I undo closing "Acme ApS"
    And the time is "2026-12-20T10:00:00Z"
    And the closing of organisations runs
    Then "Acme ApS" still has the project "Acme Portal"

  @US-184 @integration
  Scenario: Closing needs the organisation's name typed
    When I start closing "Acme ApS" typing "Acme"
    Then I see the error "Type the organisation's name to confirm"

  @US-184 @iso27001-A.5.15 @integration
  Scenario: A developer cannot export or close
    When "Carl" tries to export everything
    Then "Carl" is refused
