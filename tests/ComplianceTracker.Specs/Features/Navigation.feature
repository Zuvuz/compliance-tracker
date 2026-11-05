Feature: The main pages are in the top bar

  # US-045: Projects, Frameworks, Users and Settings in the top bar; the organisation page has no tabs.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-045 @integration
  Scenario: The organisation admin sees all the main pages
    When I open the front page
    Then the top bar has the links "Projects", "Frameworks", "Users" and "Settings"
    And the page has no tab "Overview"

  @US-045 @iso27001-A.5.15 @integration
  Scenario: A developer does not see Users and Settings
    Given "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the front page
    Then the top bar has the links "Projects" and "Frameworks"
    And the top bar has no link "Users"

  @US-045 @iso27001-A.5.15 @integration
  Scenario: A developer cannot open the users page directly
    Given "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the address "/Users"
    Then access is denied

  @US-045 @iso27001-A.5.15 @integration
  Scenario: A developer cannot open the settings page directly
    Given "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the address "/Settings"
    Then access is denied

  @US-045 @integration
  Scenario: The front page after sign-in is the projects page
    When I open the front page
    Then I am on the page "/Projects"

  @US-045 @integration
  Scenario: Old addresses still work
    When I open the old users address of "Acme ApS"
    Then I am on the page "/Users"
