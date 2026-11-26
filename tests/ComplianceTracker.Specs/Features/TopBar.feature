Feature: A top bar with one clear place for each page

  # US-225: the top bar has five items (Home, Projects, Compliance, ISMS, Organisation), each with one row of subpages.
  # The organisation area (US-142) is no longer shown as a project with its own tabs: its pages are under Compliance, its
  # risks under ISMS. Old addresses still work.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-225 @usability @integration
  Scenario: The top bar has five items
    When I open the front page
    Then the top bar has exactly "Home", "Projects", "Compliance", "ISMS" and "Organisation"

  @US-225 @usability @integration
  Scenario: Each top bar item has one row of subpages without two of the same name
    When I open every subpage of "Compliance", "ISMS" and "Organisation"
    Then no page has more than one row of subpages
    And no row has two subpages with the same name

  @US-225 @usability @integration
  Scenario: The organisation's requirements are a Compliance page, not a project
    When I open the Compliance page "Requirements"
    Then the top bar marks "Compliance"
    And the Compliance page "Requirements" is marked
    And the page has no project tabs

  @US-225 @usability @integration
  Scenario: An old address leads to the new place
    When I open the address "/Organisation?tab=nis2-scope"
    Then the top bar marks "Compliance"
    And the Compliance page "NIS2 scope" is marked
