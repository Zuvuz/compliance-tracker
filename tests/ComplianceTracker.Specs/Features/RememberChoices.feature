Feature: Remember my choices on the pages

  # US-175: the last tab of a project and the filters of the Compliance tab, the risk register and the activity log are
  # remembered in the member's browser (site.js); an address with its own filters wins, and "Reset view" forgets them.
  # These scenarios check what the pages give the script; the script itself runs in the browser.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-175 @integration
  Scenario: A project page says its tab and the project's valid tabs
    When I open the "Risks" tab of "Acme Portal"
    Then the project's navigation says the current tab is "risks" among its valid tabs

  @US-175 @integration
  Scenario: The filters that can be remembered are marked, with a way to reset them
    When I open the "Risks" tab of "Acme Portal"
    Then the filters "risk-filter" are remembered with "status,level,treatment,due,nothing" and can be reset
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the filters "compliance-filters" are remembered with "show,q" and can be reset
