Feature: No organisation can reach another organisation's data

  # US-086: every controller action is found by itself, called by a member of another organisation with the ids of
  # Acme ApS's data, and must refuse: a page or file gives "not found" or "forbidden", and a change changes nothing
  # anywhere in the database. A new action is tested without anyone remembering to add it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And I have added the evidence "MFA config" with the file "mfa.pdf" to "I.2(d)"
    And the risk "Phishing" exists in "Acme Portal"
    And the risk "Phishing" has the measure "[US-004] Sign in"
    And I have added the action "Phishing training" to the risk "Phishing"
    And "Acme Portal" has a "Trivy" scan with the findings "CVE-2026-1111" and "CVE-2026-2222"
    And I have created the upload token "GitHub Actions" for "Acme Portal"
    And I have recorded the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-01T08:00:00Z"
    And "Acme Portal" has version 1 of "kravspec.xlsx" with "REQ-001 Log in"
    And the framework "Security policy" has a draft with "A.5.15 Access control"
    And "Carl" has joined "Acme ApS" as "Developer"
    And I have created an invitation link with the role "Auditor"
    And "Bo" has created the organisation "Beta ApS"

  @US-086 @iso27001-A.5.15 @iso27001-A.8.3 @asvs-V4.2.1 @integration
  Scenario: Every action refuses another organisation's ids
    When "Bo" calls every action with the ids of "Acme ApS"
    Then every page and file gives "not found" or "forbidden"
    And no change changed anything in the database
    And every id the actions take was one of Acme ApS's

  @US-086 @integration
  Scenario: The same addresses work for the organisation's own member
    When "Anna" opens every page and file with the ids of "Acme ApS"
    Then they all work

  @US-086 @iso27001-A.5.15 @integration
  Scenario: The live updates cannot be followed for another organisation's project
    When "Bo" follows the live updates of "Acme Portal"
    Then the hub refuses
