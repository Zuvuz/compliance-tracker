Feature: AI suggestions for the organisation's documents

  # US-136 R2: when a policy, procedure or risk table is uploaded, the local AI suggests which requirements it proves, as
  # for a project's evidence (US-059). The suggestion belongs to the document and is decided once; approving links the
  # document, and it becomes evidence wherever the requirement is followed.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme App" exists
    And "Acme App" uses "CRA – Regulation (EU) 2024/2847"
    And the minimum score for evidence suggestions is 0.5

  @US-136 @iso27001-A.5.1 @integration
  Scenario: Uploading a policy gives AI suggestions, best first
    Given the AI rates "Access control policy" against "I.2(e)" with 0.61
    And the AI rates "Access control policy" against "I.2(d)" with 0.82
    When I upload the policy "Access control policy" version "v1" for the organisation
    Then the AI says on the Documents page "The AI suggests 2 requirements for the document"
    And "Access control policy" has the AI suggestions "I.2(d)" and "I.2(e)"
    And the activity log has "DocumentLinkSuggested" saying "2 suggestions for Access control policy (fake-embed, document-v1)"

  @US-136 @iso27001-A.5.1 @integration
  Scenario: Approving a suggestion makes the document evidence in every project
    Given the AI rates "Access control policy" against "I.2(d)" with 0.82
    And I upload the policy "Access control policy" version "v1" for the organisation
    When I approve the AI suggestion "I.2(d)" for "Access control policy"
    Then "Acme Portal" has the evidence "Access control policy" for "I.2(d)" from the organisation
    And "Acme App" has the evidence "Access control policy" for "I.2(d)" from the organisation
    And "Access control policy" has no AI suggestions
    And the activity log has "DocumentLinkApproved" saying "Access control policy ↔ I.2(d)"

  @US-136 @integration
  Scenario: A rejected suggestion needs a reason and is never suggested again
    Given the AI rates "Access control policy" against "I.2(d)" with 0.82
    And I upload the policy "Access control policy" version "v1" for the organisation
    When I reject the AI suggestion "I.2(d)" for "Access control policy" without a reason
    Then I see the error "Choose a reason"
    When I reject the AI suggestion "I.2(d)" for "Access control policy" because "NotRelevant"
    Then "Access control policy" has no AI suggestions
    And "Acme Portal" has no evidence "Access control policy"
    When I ask the AI for more requirements for "Access control policy"
    Then I see the error "There are no more requirements to suggest"

  @US-136 @nis2-art21 @integration
  Scenario: A policy is suggested for NIS2 in the organisation area
    Given I have done the NIS2 scope check as an important entity
    And the AI rates "Incident response plan" against "Art. 21(2)(b)" with 0.9
    When I upload the policy "Incident response plan" version "v1" for the organisation
    And I approve the AI suggestion "Art. 21(2)(b)" for "Incident response plan"
    Then the organisation's Compliance tab for "NIS2" shows "Art. 21(2)(b)" as "Covered"

  @US-136 @integration
  Scenario: Without the AI the document is still saved
    Given the local AI is not available
    When I upload the policy "Access control policy" version "v1" for the organisation
    Then the AI says on the Documents page "The document is saved, but there are no AI suggestions"
    And the document "Access control policy" is used by "no project yet"

  @US-136 @integration
  Scenario: A new version replaces the pending suggestions instead of adding to them
    Given the AI rates "Access control policy" against "I.2(d)" with 0.82
    And I upload the policy "Access control policy" version "v1" for the organisation
    When I upload version "v2" of "Access control policy" with the content "Updated text"
    Then "Access control policy" has the AI suggestions "I.2(d)"

  @US-136 @integration
  Scenario: Waiting suggestions show under "Needs attention"
    Given the AI rates "Access control policy" against "I.2(d)" with 0.82
    And I upload the policy "Access control policy" version "v1" for the organisation
    Then "Needs attention" of "Acme Portal" shows "1 AI suggestion for the organisation's documents wait for a decision"

  @US-136 @integration
  Scenario: Only members who manage frameworks decide on the suggestions
    Given the AI rates "Access control policy" against "I.2(d)" with 0.82
    And I upload the policy "Access control policy" version "v1" for the organisation
    And "Bo" is in "Acme ApS" with the role "Developer"
    And I am signed in as "Bo"
    When I send an approval of the AI suggestion "I.2(d)" for "Access control policy" anyway
    Then "Acme Portal" has no evidence "Access control policy"
