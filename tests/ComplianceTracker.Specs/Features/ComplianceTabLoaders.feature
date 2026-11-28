Feature: Compliance and the requirement page have their own loaders

  # US-232: the Compliance tab and a requirement's page under it each have a loader of their own (the foundation is
  # US-229). The pages look as before; an unknown sub-tab, requirement or another organisation's project is not found.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-232 @iso27001-A.8.25 @integration
  Scenario: A requirement's page shows its evidence and risks
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    And the risk "Stolen session cookie" has the requirement "I.2(d)" as a measure
    And I add the evidence "Access policy" with the link "https://example.com/policy" to "I.2(d)"
    When I open the address of "I.2(d)" in "Acme Portal"
    Then the row "I.2(d)" shows the evidence "Access policy"
    And the row "I.2(d)" names the risk "Stolen session cookie"

  @US-232 @iso27001-A.8.28 @integration
  Scenario: An unknown requirement is not found
    When I open the address of "X.99" in "Acme Portal"
    Then the page is not found

  @US-232 @iso27001-A.8.28 @integration
  Scenario: An unknown framework sub-tab is not found
    When I open the address "/Projects/Details/{id}/compliance/unknown-framework" of "Acme Portal"
    Then the page is not found

  @US-232 @iso27001-A.8.28 @integration
  Scenario: The "All" sub-tab is not found with only one framework
    When I open the address "/Projects/Details/{id}/compliance/all" of "Acme Portal"
    Then the page is not found

  @US-232 @iso27001-A.8.28 @integration
  Scenario: Another organisation's requirement page is not found
    Given "Bo" has created the organisation "Other ApS"
    When "Bo" opens the address "/Projects/Details/{id}/compliance/cra/I.2(d)" of "Acme Portal"
    Then the page is not found
