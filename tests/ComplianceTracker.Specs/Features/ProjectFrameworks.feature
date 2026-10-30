Feature: Use the organisation's frameworks in projects

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has been uploaded
    And the projects "Acme Portal" and "Acme App" exist

  @US-011 @iso27001-A.5.31 @integration
  Scenario: Choose frameworks when creating a project
    When I create the project "Acme Shop" using "Security policy"
    Then the project "Acme Shop" uses "Security policy" version 1

  @US-011 @iso27001-A.5.31 @integration
  Scenario: Add a framework to an existing project
    When I add "Security policy" to the project "Acme Portal"
    Then the project "Acme Portal" uses "Security policy" version 1

  @US-011 @iso27001-7.5.3 @integration
  Scenario: Choose which projects move to a new version
    Given "Acme Portal" and "Acme App" use "Security policy" version 1
    When I upload a new version of "Security policy" for the project "Acme Portal"
    Then the project "Acme Portal" uses "Security policy" version 2
    And the project "Acme App" uses "Security policy" version 1

  @US-011 @iso27001-7.5.3 @integration
  Scenario: The organisation page shows outdated projects
    Given "Acme Portal" uses "Security policy" version 2
    And "Acme App" uses "Security policy" version 1
    When I open the organisation page
    Then I see "Security policy" version 2 used by "Acme Portal"
    And I see "Security policy" version 1 used by "Acme App" marked "not on newest version"

  @US-011 @iso27001-7.5.3 @integration
  Scenario: Move a project to the newest version
    Given "Acme Portal" uses "Security policy" version 2
    And "Acme App" uses "Security policy" version 1
    When I move "Acme App" to the newest version of "Security policy"
    Then the organisation page shows only "Security policy" version 2, used by "Acme Portal" and "Acme App"

  @US-011 @integration
  Scenario: A project uses only one version of a framework
    Given "Acme Portal" uses "Security policy" version 2
    When I add "Security policy" version 1 to the project "Acme Portal"
    Then I see the error "The project already uses Security policy"

  @US-011 @integration
  Scenario: Remove a framework from a project
    Given "Acme App" uses "Security policy" version 1
    When I remove "Security policy" from the project "Acme App"
    Then the project "Acme App" does not use "Security policy"

  @US-011 @integration
  Scenario: A built-in framework in use cannot be switched off
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I switch "CRA – Regulation (EU) 2024/2847" off
    Then I see the error "Used by projects: Acme Portal"

  @US-011 @iso27001-A.8.15 @integration
  Scenario: Changes are logged
    Given "Acme App" uses "Security policy" version 1
    And a new version of "Security policy" has been uploaded
    When I move "Acme App" to the newest version of "Security policy"
    Then the activity log for "Acme ApS" has "ProjectFrameworkVersionChanged" for "Anna" concerning "Acme App"
