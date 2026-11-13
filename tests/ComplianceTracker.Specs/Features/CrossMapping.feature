Feature: Cross-mapping between frameworks

  # US-037: the AI suggests which requirements in two frameworks the organisation follows ask for the same thing, as
  # Same, Partly or Related. Nothing counts until a person approves it; an approved "Same" shows a requirement as covered
  # through the other one, so work is done once. The AI is the pretend one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Users must sign in securely" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"

  @US-037 @aiact-art14 @iso27001-A.5.36 @integration
  Scenario: The AI suggests matches with their strength
    Given the AI rates "SP-1 Users must sign in securely" against "I.2(d)" with 0.9
    And the AI rates "SP-2 Backups" against "I.2(d)" with 0.75
    When I ask the AI to match "Security policy" with "CRA"
    Then the cross-mapping suggests "SP-1" and "I.2(d)" as "Same"
    And the cross-mapping suggests "SP-2" and "I.2(d)" as "Partly"
    And every cross-mapping suggestion is marked "AI-generated"
    And the activity log has "MappingsSuggested" with "2 cross-mapping suggestions between Security policy v1 and CRA"

  @US-037 @aiact-art14 @integration
  Scenario: An approved Same match passes the coverage on
    Given the AI rates "SP-1 Users must sign in securely" against "I.2(d)" with 0.9
    And I ask the AI to match "Security policy" with "CRA"
    When I approve the cross-mapping of "SP-1" and "I.2(d)"
    Then the approved matches list "SP-1" and "I.2(d)"
    And the requirement "SP-1" of "Acme Portal" is covered through "CRA I.2(d)"
    And the activity log has "MappingApproved" with "Cross-mapping Same: Security policy SP-1 ↔ CRA I.2(d)"

  @US-037 @aiact-art14 @integration
  Scenario: An approved Partly match does not count as coverage
    Given the AI rates "SP-1 Users must sign in securely" against "I.2(d)" with 0.75
    And I ask the AI to match "Security policy" with "CRA"
    When I approve the cross-mapping of "SP-1" and "I.2(d)"
    Then the requirement "SP-1" of "Acme Portal" is not covered

  @US-037 @aiact-art14 @integration
  Scenario: Rejecting needs a reason and a rejected match is not suggested again
    Given the AI rates "SP-1 Users must sign in securely" against "I.2(d)" with 0.9
    And I ask the AI to match "Security policy" with "CRA"
    When I reject the cross-mapping of "SP-1" and "I.2(d)" without a reason
    Then I see the error "Choose a reason"
    And the cross-mapping suggests "SP-1" and "I.2(d)" as "Same"
    When I reject the cross-mapping of "SP-1" and "I.2(d)" because "WrongRequirement"
    And I ask the AI to match "Security policy" with "CRA"
    Then the cross-mapping page says "The AI found no requirements that ask for the same thing"
    And the activity log has "MappingRejected" with "Cross-mapping: Security policy SP-1 ↔ CRA I.2(d) (WrongRequirement)"

  @US-037 @aiact-art14 @integration
  Scenario: Without the local AI nothing is suggested
    Given the local AI is not available
    When I ask the AI to match "Security policy" with "CRA"
    Then I see the error "The local AI cannot be reached right now, so nothing was compared"
    And the cross-mapping has no suggestions
