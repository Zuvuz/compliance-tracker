Feature: A rejection holds until what was judged changes

  # US-145: a rejected AI suggestion stays away while neither side has changed. A new version of the document (or a new
  # text of the requirement) lets the pair come back, with a note about the earlier rejection. A person can also undo it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the minimum score for evidence suggestions is 0.5
    And the AI rates "Access control policy" against "I.2(d)" with 0.82
    And I upload the policy "Access control policy" version "v1" for the organisation
    And I reject the AI suggestion "I.2(d)" for "Access control policy" because "NotRelevant"

  @US-145 @integration
  Scenario: An unchanged pair is never suggested again
    When I ask the AI for more requirements for "Access control policy"
    Then I see the error "There are no more requirements to suggest"
    And "Access control policy" has no AI suggestions

  @US-145 @integration
  Scenario: A new version of the document lets the pair come back
    When I upload version "v2" of "Access control policy" with the content "Updated text"
    Then "Access control policy" has the AI suggestions "I.2(d)"
    And the AI suggestion "I.2(d)" for "Access control policy" says "Rejected before by Anna"
    And the AI suggestion "I.2(d)" for "Access control policy" says "The document has changed since"

  @US-145 @integration
  Scenario: A person can undo a rejection
    When I undo the rejection of "I.2(d)" for "Access control policy"
    And I ask the AI for more requirements for "Access control policy"
    Then "Access control policy" has the AI suggestions "I.2(d)"
    And the activity log has "SuggestionRejectionUndone" saying "Document Access control policy ↔ I.2(d)"
