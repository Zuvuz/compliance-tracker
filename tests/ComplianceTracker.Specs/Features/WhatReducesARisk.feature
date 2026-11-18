Feature: What reduces a risk

  # US-202: one section on a risk's page shows what reduces the risk: its measures (requirements and user stories), each
  # with the actions that carry it out, then "Other actions". The full thread (US-124) is folded into the same section.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the risk "Session Hijacking" exists in "Acme Portal"

  @US-202 @iso27001-6.1.3 @integration
  Scenario: An action is added under the requirement it carries out
    Given the risk "Session Hijacking" has the requirement "I.2(d)" as a measure
    When I add the action "Better passwords and cookie limit" under "I.2(d)" on "Session Hijacking"
    Then "What reduces this risk" shows "I.2(d)" with the action "Better passwords and cookie limit"
    And the page has no section "Trace" or "Treatment actions"

  @US-202 @integration
  Scenario: An action without a measure is under "Other actions" and can be moved
    Given the risk "Session Hijacking" has the requirement "I.2(d)" as a measure
    And I have added the action "Train the staff" to the risk "Session Hijacking"
    When I move the action "Train the staff" of "Session Hijacking" under "I.2(d)"
    Then "What reduces this risk" shows "I.2(d)" with the action "Train the staff"

  @US-202 @integration
  Scenario: A requirement with actions cannot be removed
    Given the risk "Session Hijacking" has the requirement "I.2(d)" as a measure
    And I add the action "Better passwords and cookie limit" under "I.2(d)" on "Session Hijacking"
    When I remove the measure "I.2(d)" from "Session Hijacking"
    Then the risk page says "Move or remove the 1 action under the measure first"
    And the risk "Session Hijacking" has the measure "I.2(d)"

  @US-202 @integration
  Scenario: The requirements that share words with the risk come first
    Given the risk "Unauthorised access to the portal" exists in "Acme Portal"
    When I open the risk "Unauthorised access to the portal"
    Then the most relevant requirement offered as a measure is "I.2(d)"

  @US-202 @integration
  Scenario: An accepted risk needs no actions
    Given I accept the risk "Session Hijacking" with the reason "Low value target"
    When I open the risk "Session Hijacking"
    Then I see "No actions needed: the risk is accepted"
