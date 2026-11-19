Feature: Cross-mapping across several frameworks, for chosen projects

  # US-206: two or more frameworks are matched in one run, the matches are shown per requirement so one decision covers
  # them all, and an approved match can be limited to chosen projects. The AI is the pretend one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the framework "Access rules" has the active requirements "AR-1 Admins use MFA" and "AR-2 Logs are kept"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" uses "Security policy" version 1
    And "Acme Portal" uses "Access rules" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"

  @US-206 @iso27001-6.1.3 @integration
  Scenario: Three frameworks are matched in one run and shown per requirement
    Given the AI rates "SP-1 Users must sign in securely" against "AR-1" with 0.75
    And the AI rates "SP-1 Users must sign in securely" against "I.2(d)" with 0.65
    When I ask the AI to match "Security policy", "Access rules" and "CRA" at once
    Then the matches of "SP-1" are "Access rules AR-1, CRA I.2(d)"
    And the activity log has "MappingsSuggested" with "2 cross-mapping suggestions between Security policy v1, Access rules v1 and CRA v"

  @US-206 @iso27001-6.1.3 @integration
  Scenario: One decision approves all of a requirement's matches
    Given the AI rates "SP-1 Users must sign in securely" against "AR-1" with 0.75
    And the AI rates "SP-1 Users must sign in securely" against "I.2(d)" with 0.65
    And I ask the AI to match "Security policy", "Access rules" and "CRA" at once
    When I approve all matches of "SP-1"
    Then the approved matches across the frameworks are "SP-1 ↔ AR-1, SP-1 ↔ I.2(d)"
    And nothing waits for a decision across the frameworks
    And the activity log has "MappingApproved" with "Cross-mapping Partly: Security policy SP-1 ↔ Access rules AR-1"

  @US-206 @iso27001-6.1.3 @integration
  Scenario: A match limited to one project carries coverage only there
    Given the project "Acme App" exists
    And "Acme App" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme App" uses "Security policy" version 1
    And the AI rates "SP-1 Users must sign in securely" against "I.2(d)" with 0.9
    And I ask the AI to match "Security policy" with "CRA"
    And I approve the cross-mapping of "SP-1" and "I.2(d)"
    And the requirement "SP-1" of "Acme Portal" is covered through "CRA I.2(d)"
    When I limit the match of "SP-1" and "I.2(d)" to "Acme App"
    Then the match of "SP-1" and "I.2(d)" applies to "Acme App"
    And the requirement "SP-1" of "Acme Portal" is not covered
    And the activity log has "MappingProjectsChanged" with "Security policy SP-1 ↔ CRA I.2(d): Acme App"
