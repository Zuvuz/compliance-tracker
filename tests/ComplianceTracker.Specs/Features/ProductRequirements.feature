Feature: Product requirements tab

  # US-104: the tab that was called "User stories" is called "Product requirements". The project chooses how it describes
  # its requirements (GitHub user stories, a requirement specification, use cases), and only those are shown.
  # Turning a method off deletes nothing.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-104 @integration
  Scenario: The tab is called Product requirements
    When I open the "Overview" tab of "Acme Portal"
    Then the project has the tab "Product requirements"
    And it has no tab "User stories"

  @US-104 @integration
  Scenario: The old address still works
    When I open the old user stories address of "Acme Portal"
    Then I am on the "Product requirements" tab of "Acme Portal"

  @US-104 @integration
  Scenario: A new project uses every method until one is chosen
    When I open the "Product requirements" tab of "Acme Portal"
    Then the tab shows the sections "GitHub user stories", "Requirement specification" and "Use cases"

  @US-104 @iso27001-A.8.25 @integration
  Scenario: Only the chosen methods are shown
    When I choose the methods "Use cases" for "Acme Portal"
    Then the tab shows the section "Use cases"
    And the tab does not show the section "GitHub user stories"
    And the tab does not show the section "Requirement specification"

  @US-104 @integration
  Scenario: At least one method is needed
    When I choose no methods for "Acme Portal"
    Then I see the error "Choose at least one method"

  @US-104 @integration
  Scenario: Turning a method off keeps its links
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    When I choose the methods "Requirement specification" for "Acme Portal"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" is "Covered" by "[US-004] Sign in"

  @US-104 @integration
  Scenario: Turning a method on again shows its items again
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And I have chosen the methods "Use cases" for "Acme Portal"
    When I choose the methods "GitHub user stories" and "Use cases" for "Acme Portal"
    Then the tab lists the work item "[US-004] Sign in"

  @US-104 @iso27001-A.5.15 @integration
  Scenario: Only roles that edit projects can change the methods
    Given "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the "Product requirements" tab of "Acme Portal"
    Then "Bo" cannot change the methods

  @US-104 @iso27001-A.8.15 @integration
  Scenario: Changing the methods is logged
    When I choose the methods "GitHub user stories" and "Use cases" for "Acme Portal"
    Then the activity log for "Acme ApS" has "ProjectMethodsChanged" for "Anna" concerning "Acme Portal"

  @US-104 @integration
  Scenario: Existing projects get the methods they already use
    Given a project linked to GitHub was made before the methods existed
    When the database with that project is migrated
    Then that project uses only "GitHub user stories"
