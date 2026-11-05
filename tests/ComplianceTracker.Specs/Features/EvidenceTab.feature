Feature: Evidence tab

  # US-059: evidence belongs to the project and is added once on the Evidence tab. It is linked to every requirement it
  # proves, and the local AI suggests which ones; a person approves or rejects each suggestion.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-059 @iso27001-7.5.3 @integration
  Scenario: Upload evidence once on the Evidence tab
    When I upload the evidence "Access policy 2026" with the file "access-policy.pdf" on the Evidence tab of "Acme Portal"
    Then the Evidence tab lists "Access policy 2026" with its SHA-256

  @US-059 @cra-annex7 @integration
  Scenario: One evidence proves several requirements
    Given the evidence "Access policy 2026" is on the Evidence tab of "Acme Portal"
    When I link "Access policy 2026" to the requirements "I.2(d)" and "I.2(b)"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" shows the evidence "Access policy 2026"
    And the row "I.2(b)" shows the evidence "Access policy 2026"

  @US-059 @aiact-art14 @integration
  Scenario: The AI suggests which requirements the evidence proves
    Given the AI rates "Access policy 2026" against "I.2(d)" with 0.81
    When I upload the evidence "Access policy 2026" with the file "access-policy.pdf" on the Evidence tab of "Acme Portal"
    Then "Access policy 2026" has the suggestion "I.2(d)" with score "0.81" marked "AI-generated"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" does not show the evidence "Access policy 2026"

  @US-059 @aiact-art14 @integration
  Scenario: Approving a suggestion links the evidence
    Given "Access policy 2026" has the suggestion "I.2(d)"
    When I approve the evidence suggestion "I.2(d)" for "Access policy 2026"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" shows the evidence "Access policy 2026"
    And the activity log for "Acme ApS" has "EvidenceLinkApproved" for "Anna" concerning "Acme Portal"

  @US-059 @integration
  Scenario: A rejected suggestion is not made again
    Given "Access policy 2026" has the suggestion "II.1"
    And I reject the evidence suggestion "II.1" for "Access policy 2026" with the reason "NotRelevant"
    When I ask for more evidence suggestions for "Access policy 2026"
    Then "Access policy 2026" has no evidence suggestion "II.1"

  @US-059 @iso27001-A.8.15 @integration
  Scenario: Removing evidence takes it off every requirement
    Given the evidence "Access policy 2026" is linked to "I.2(d)" and "I.2(b)"
    When I remove the evidence "Access policy 2026" on the Evidence tab
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the rows "I.2(d)" and "I.2(b)" no longer show "Access policy 2026"
    And the removed evidence "Access policy 2026" is still stored

  @US-059 @integration
  Scenario: Taking evidence off one requirement keeps it on the others
    Given the evidence "Access policy 2026" is linked to "I.2(d)" and "I.2(b)"
    When I remove the evidence "Access policy 2026" from "I.2(d)"
    Then the row "I.2(b)" shows the evidence "Access policy 2026"
    And the row "I.2(d)" does not show the evidence "Access policy 2026"

  @US-059 @integration
  Scenario: Existing evidence can be picked on the Compliance tab
    Given the evidence "Access policy 2026" is on the Evidence tab of "Acme Portal"
    When I pick the existing evidence "Access policy 2026" for "I.2(d)"
    Then the row "I.2(d)" shows the evidence "Access policy 2026"

  @US-059 @integration
  Scenario: Evidence from US-057 is kept
    Given evidence "PR 42" was added to "I.2(d)" before the Evidence tab existed
    When that database is migrated
    Then evidence "PR 42" is linked to "I.2(d)" by the same person at the same time

  @US-059 @iso27001-A.5.15 @integration
  Scenario: An auditor sees the Evidence tab but cannot change it
    Given the evidence "Access policy 2026" is linked to "I.2(d)" and "I.2(b)"
    And "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the Evidence tab of "Acme Portal"
    Then "Bo" sees "Access policy 2026" linked to "I.2(d)" and "I.2(b)"
    And there is no "Approve" button
    And there is no "Add evidence" button
