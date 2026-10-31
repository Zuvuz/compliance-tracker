Feature: Edit a project

  # US-024: a project's details can be corrected and it can move between lifecycle phases.
  # EndOfSupport is final: such a project can only be viewed.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the projects "Acme Portal" and "Acme App" exist

  @US-024 @iso27001-A.5.9 @integration
  Scenario: Edit a project's details
    When I change the scope of "Acme Portal" to "Web shop only"
    Then the page of "Acme Portal" shows scope "Web shop only"

  @US-024 @iso27001-A.5.9 @integration
  Scenario: Rename a project
    When I rename "Acme Portal" to "Acme Customer Portal"
    Then the organisation page lists the project "Acme Customer Portal"

  @US-024 @integration
  Scenario: The name stays unique
    When I rename "Acme Portal" to "Acme App"
    Then I see the error "Project name already in use"
    And the name field still contains "Acme App"

  @US-024 @integration
  Scenario: The intended purpose is still required
    When I change the intended purpose of "Acme Portal" to ""
    Then I see the error "Intended purpose is required"

  @US-024 @iso27001-A.5.9 @integration
  Scenario: Move a project to the next phase
    When I change the phase of "Acme Portal" to "Development"
    Then the page of "Acme Portal" shows phase "Development"

  @US-024 @integration
  Scenario: A project can move back to an earlier phase
    Given "Acme Portal" is in phase "Released"
    When I change the phase of "Acme Portal" to "Development"
    Then the page of "Acme Portal" shows phase "Development"

  @US-024 @iso27001-A.8.15 @integration
  Scenario: Phase changes are logged with the old and new phase
    When I change the phase of "Acme Portal" to "Development"
    Then the activity log for "Acme ApS" has "ProjectPhaseChanged" for "Anna" concerning "Acme Portal"
    And the "ProjectPhaseChanged" entry says "Phase: Planning → Development"

  @US-024 @iso27001-A.8.15 @integration
  Scenario: Edits are logged with the old and new value
    When I change the scope of "Acme Portal" to "Web shop only"
    Then the activity log for "Acme ApS" has "ProjectUpdated" for "Anna" concerning "Acme Portal"
    And the "ProjectUpdated" entry says "Scope: (empty) → Web shop only"

  @US-024 @iso27001-A.5.9 @integration
  Scenario: A project at end of support cannot be changed
    Given "Acme App" is in phase "EndOfSupport"
    When I change the scope of "Acme App" to "Anything"
    Then I see the error "A project at end of support cannot be changed"
    And the page of "Acme App" has no Edit button and no phase form

  @US-024 @integration
  Scenario: End of support is final
    Given "Acme App" is in phase "EndOfSupport"
    When I change the phase of "Acme App" to "Released"
    Then I see the error "A project at end of support cannot be changed"
    And the page of "Acme App" shows phase "EndOfSupport"

  @US-024 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot edit our projects
    Given "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the edit page of "Acme Portal"
    Then the page is not found
