Feature: The requirement page

  # US-153: the Compliance tab is a light overview of every requirement with what is linked to it; one requirement opens
  # on its own page (its address from US-144 R12), where every action is. After an action the same page is shown.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-153 @integration
  Scenario: The overview shows what is linked without forms
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the overview row "I.2(d)" says "1 story · 0 evidence" and "Covered"
    And the overview has no forms in its rows

  @US-153 @integration
  Scenario: The overview shows failing tests
    When I upload test results where 2 scenarios tagged "@cra-I.2(d)" passed and 1 failed
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the overview row "I.2(d)" shows "1 failing test"

  @US-153 @integration
  Scenario: A requirement opens on its own page with its actions
    When I open the requirement "I.2(d)" from the overview
    Then I am on the page of "I.2(d)"
    And the page has the primary action "Add evidence" and the actions "Ask the AI" and "Not applicable" under "More"

  @US-153 @integration
  Scenario: After an action the same requirement is shown
    When I add the evidence "Access policy" with the link "https://example.com/policy" to "I.2(d)"
    Then I am on the page of "I.2(d)"
    And the row "I.2(d)" shows the evidence "Access policy"

  @US-153 @integration
  Scenario: An error from an action is shown on the requirement's page
    When I add the evidence "Access policy" with the link "not a link" to "I.2(d)"
    Then I am on the page of "I.2(d)"
    And the page shows an error

  @US-153 @integration
  Scenario: A requirement that does not exist is not found
    When I open the address of "X.99" in "Acme Portal"
    Then the page is not found

  @US-153 @integration
  Scenario: The Compliance tab stays light with realistic data
    Given "Acme Portal" has 30 user stories, 12 risks and 20 pieces of evidence on its requirements
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the page is under 150 KB
    And the overview has no forms in its rows
