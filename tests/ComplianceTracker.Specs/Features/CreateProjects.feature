Feature: Create projects

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-008 @iso27001-A.5.9 @cra-annexVII-1a @integration
  Scenario: Create a project
    When I create the project "Acme Portal" with intended purpose "Web portal where customers manage their orders"
    Then the organisation page lists the project "Acme Portal"

  @US-008 @integration
  Scenario: Intended purpose is required
    When I create the project "Acme Portal" with no intended purpose
    Then I see the error "Intended purpose is required"

  @US-008 @integration
  Scenario: Project names are unique in the organisation
    Given the project "Acme Portal" exists
    When I create the project "Acme Portal" with intended purpose "Something else"
    Then I see the error "Project name already in use"

  @US-008 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see our projects
    Given the project "Acme Portal" exists
    And "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the page of "Acme Portal"
    Then the page is not found

  @US-008 @iso27001-A.8.15 @integration
  Scenario: Creating a project is logged
    When I create the project "Acme Portal" with intended purpose "Web portal where customers manage their orders"
    Then the activity log for "Acme ApS" has "ProjectCreated" for "Anna" concerning "Acme Portal"

  @US-008 @US-016 @integration
  Scenario: Project name is required
    When I create the project "" with intended purpose "Web portal"
    Then I see the error "Project name is required"

  @US-008 @US-016 @integration
  Scenario: All project fields are stored and shown
    When I create the project "Acme Portal" with description "Customer portal", intended purpose "Orders", scope "Web shop only" and phase "Development"
    Then the page of "Acme Portal" shows description "Customer portal", scope "Web shop only" and phase "Development"

  @US-008 @US-016 @iso27001-A.8.15 @integration
  Scenario: Creating a project is logged with its time
    Given the time is "2026-10-24T14:02:00Z"
    When I create the project "Acme Portal" with intended purpose "Web portal"
    Then the activity log for "Acme ApS" has "ProjectCreated" at "2026-10-24T14:02:00Z"
