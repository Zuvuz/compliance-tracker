Feature: Evidence check with AI

  # US-038: "Check with AI" on a piece of evidence asks the local AI whether it covers the requirement and what is
  # missing. The verdict is "AI-generated" until a person who decides compliance confirms or overrules it; evidence a
  # person agrees does not cover the requirement no longer counts as its evidence.

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

  @US-038 @aiact-art14 @iso27001-A.5.36 @integration
  Scenario: The verdict is shown as AI-generated
    Given the AI finds that the evidence on "I.2(d)" partly covers it and misses "the recovery codes"
    When I check the evidence "MFA config" on "I.2(d)" with AI
    Then the evidence "MFA config" on "I.2(d)" shows "AI-generated" and "Partly covers"
    And the evidence "MFA config" on "I.2(d)" says it misses "the recovery codes"
    And "I.2(d)" still has evidence
    And the activity log has "EvidenceChecked" with "I.2(d): MFA config – Partly covers (AI, "

  @US-038 @aiact-art14 @integration
  Scenario: A person confirms the verdict
    Given the AI finds that the evidence on "I.2(d)" partly covers it and misses "the recovery codes"
    And I check the evidence "MFA config" on "I.2(d)" with AI
    When I confirm the check of "MFA config" on "I.2(d)"
    Then the evidence "MFA config" on "I.2(d)" shows "Evidence partly"
    And the activity log has "EvidenceCheckDecided" with "Confirmed: Partly covers"

  @US-038 @aiact-art14 @integration
  Scenario: Evidence that does not cover the requirement no longer counts
    Given I check the evidence "MFA config" on "I.2(d)" with AI
    When I overrule the check of "MFA config" on "I.2(d)" with "DoesNotCover" because "It is the old login"
    Then the evidence "MFA config" on "I.2(d)" shows "Evidence does not cover"
    And "I.2(d)" has no evidence any more

  @US-038 @integration
  Scenario: Overruling needs a reason
    Given I check the evidence "MFA config" on "I.2(d)" with AI
    When I overrule the check of "MFA config" on "I.2(d)" with "DoesNotCover" because ""
    Then I see the error "Give a reason for overruling the AI"

  @US-038 @integration
  Scenario: Without the AI nothing is checked
    Given the local AI is not available
    When I check the evidence "MFA config" on "I.2(d)" with AI
    Then I see the error "The local AI cannot be reached right now, so nothing was checked"
    And "Acme Portal" has no evidence checks
