Feature: Each project tab has its own loader

  # US-229: the project page finds a tab's handler by its address and area; the handler reads the tab's filters, its loader
  # reads the data, and the page looks as before. The Risks tab is the first; the others follow in US-230 – US-235.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the risk "Stolen session cookie" exists in "Acme Portal"
    And the risk "Phishing" exists in "Acme Portal"
    And the risk "Old TLS on legacy API" exists in "Acme Portal"
    And I close the risk "Old TLS on legacy API" with the reason "Retired"

  @US-229 @iso27001-A.8.25 @integration
  Scenario: The Risks tab shows the same risks as before
    When I open the address "/Projects/Details/{id}/risks" of "Acme Portal"
    Then the register lists 2 risks

  @US-229 @iso27001-A.8.25 @integration
  Scenario: The Risks tab's filters still work
    When I open the address "/Projects/Details/{id}/risks?status=Closed" of "Acme Portal"
    Then the register lists 1 risks

  @US-229 @iso27001-A.8.28 @integration
  Scenario: Another organisation's project is not found
    Given "Bo" has created the organisation "Other ApS"
    When "Bo" opens the address "/Projects/Details/{id}/risks" of "Acme Portal"
    Then the page is not found

  @US-229 @iso27001-A.8.25 @integration
  Scenario: An unknown tab is not found
    When I open the address "/Projects/Details/{id}/nonsense" of "Acme Portal"
    Then the page is not found
