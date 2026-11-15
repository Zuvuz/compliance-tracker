Feature: Light pages

  # US-154: every page and tab stays small with real amounts of data, so the tracker stays fast as a project grows.
  # Lists show light rows; pickers are on an item's own page, and long lists show the first items with "Show all".

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" has 30 user stories, 12 risks and 20 pieces of evidence on its requirements

  @US-154 @iso27001-A.8.6 @integration
  Scenario: Every page and tab stays under the size limit
    When I open every page and tab of "Acme Portal"
    Then each page is under 150 KB
    And no page repeats a picker on its rows
