Feature: Undo instead of "Are you sure?" for small removals

  # US-174: a small removal (a story's link to a requirement, evidence on a requirement, a measure, evidence on a story)
  # asks nothing first; the message after it offers Undo for 2 minutes. Undo makes the link again the normal way, logged.
  # Big steps keep their confirmation (US-135).

  Background:
    Given an empty database
    And the time is "2026-11-17T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"

  @US-174 @integration
  Scenario: A removed link to a story can be undone
    When I remove the link between "[US-004] Sign in" and "I.2(d)"
    Then the message says "Removed the link to [US-004] Sign in" and offers Undo
    When I press Undo
    Then the row "I.2(d)" is "Covered"
    And the activity log has "RequirementUnlinked" saying "I.2(d) ↔ [US-004] Sign in"
    And the activity log has "RequirementLinked" saying "I.2(d) ↔ [US-004] Sign in"

  @US-174 @integration
  Scenario: A removed measure can be undone
    Given the risk "Account takeover" exists in "Acme Portal"
    And the risk "Account takeover" has the requirement "I.2(d)" as a measure
    When I remove the measure "I.2(d)" from "Account takeover"
    Then the message says "Removed the measure" and offers Undo
    When I press Undo
    Then the risk "Account takeover" has the measure "I.2(d)" again

  @US-174 @integration
  Scenario: After 2 minutes the Undo is gone
    When I remove the link between "[US-004] Sign in" and "I.2(d)"
    And the time is "2026-11-17T09:03:00Z"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then no Undo is offered

  @US-174 @integration
  Scenario: A small removal asks nothing first
    When I open the requirement "I.2(d)" from the overview
    Then the form "form.unlink-requirement" asks nothing first
