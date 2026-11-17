Feature: A clearer risk page

  # US-147: the Risks tab lists the risks by level, highest first; a matrix cell filters the list; the filters are
  # buttons that keep the cell; a risk opens in a side panel with its own address, and its own page stays.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And I add the risk "Weak passwords" to "Acme Portal" with likelihood 2 and impact 2
    And I add the risk "Stolen session cookie" to "Acme Portal" with likelihood 3 and impact 4
    And I add the risk "Unauthorized remote access" to "Acme Portal" with likelihood 4 and impact 5

  @US-147 @integration
  Scenario: The register lists the risks by level, highest first
    When I open the "Risks" tab of "Acme Portal"
    Then the register lists "Unauthorized remote access", "Stolen session cookie" and "Weak passwords" in that order

  @US-147 @integration
  Scenario: A matrix cell filters the list, and again clears it
    When I open the "Risks" tab of "Acme Portal"
    And I click the matrix cell with likelihood 3 and impact 4
    Then the register lists only "Stolen session cookie"
    When I click the matrix cell with likelihood 3 and impact 4
    Then the register lists 3 risks

  @US-147 @integration
  Scenario: The filter buttons keep the matrix cell
    When I open the "Risks" tab of "Acme Portal"
    And I click the matrix cell with likelihood 3 and impact 4
    And I click the filter button "Review due"
    Then the register lists no risks

  @US-147 @integration
  Scenario: A risk opens in the side panel with its own address
    When I open the "Risks" tab of "Acme Portal" with the risk "Stolen session cookie" in the side panel
    Then the side panel is set to open "Stolen session cookie"
    And the risk's link still leads to its own page
