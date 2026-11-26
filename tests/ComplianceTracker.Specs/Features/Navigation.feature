Feature: The main pages are in the top bar

  # US-045: Projects, Frameworks, Users and Settings in the top bar; the organisation page has no tabs.
  # US-169 moved Users and Settings under Organisation. #52: Organisation is a plain link, and the organisation's pages
  # are a row of links on each of them instead of a dropdown.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-045 @integration
  Scenario: The organisation admin sees all the main pages
    When I open the front page
    Then the top bar has the links "Home", "Projects", "Compliance" and "Organisation"
    And the top bar opens no menu
    And the page has no tab "Overview"
    When I open "Organisation" from the top bar
    Then the organisation's pages have the links "Users" and "Settings"

  @US-045 @iso27001-A.5.15 @integration
  Scenario: A developer does not see Users and Settings
    Given "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the front page
    Then the top bar has the links "Projects" and "Compliance"
    And the top bar has no link "Users"
    When "Bo" opens the organisation's pages
    Then the organisation's pages have no link "Users"

  @US-045 @iso27001-A.5.15 @integration
  Scenario: A developer cannot open the users page directly
    Given "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the address "/Users"
    Then access is denied

  # US-163 R3 (changes US-045 R2): a developer sees the settings read-only; the forms do not work for them
  @US-045 @US-163 @iso27001-A.5.15 @integration
  Scenario: A developer sees the settings page directly, without changing anything
    Given "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the address "/Settings"
    Then the answer is the page and says "You can see the settings but not change them."

  @US-045 @integration
  Scenario: The open organisation page is marked
    When I open the organisation's page "Settings"
    Then the organisation's page "Settings" is marked
    And the top bar marks "Organisation"

  @US-045 @integration
  Scenario: The front page after sign-in is the projects page
    When I open the front page
    Then I am on the page "/Projects"

  @US-045 @integration
  Scenario: Old addresses still work
    When I open the old users address of "Acme ApS"
    Then I am on the page "/Users"
