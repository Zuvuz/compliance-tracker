Feature: Design rules and shared components

  # US-168: every main page starts with the shared page header (title, status and at most one primary action), and a
  # status always has the same colour (docs/design-rules.md).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the risk "Account takeover" exists in "Acme Portal"

  @US-168 @integration
  Scenario: The main pages have the shared header with at most one primary action
    When I open each main page of "Acme Portal"
    Then each page has the shared header with at most one primary action

  @US-168 @integration
  Scenario: A status has the same colour on the overview and on the requirement's page
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the overview row "I.2(d)" has the status "Not covered" in red
    When I open the requirement "I.2(d)" from the overview
    Then the requirement's page has the status "Not covered" in red
