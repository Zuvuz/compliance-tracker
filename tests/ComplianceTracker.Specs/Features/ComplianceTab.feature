Feature: Compliance tab: requirements, links and coverage per regulation

  # US-019: the project's requirements. US-021: which user stories cover them and the coverage.
  # US-041: shown on a Compliance tab with one sub-tab per regulation.
  # Links are made by hand for now; AI suggestions (US-020) come later.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Passwords" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in with OpenID Connect"

  @US-019 @iso27001-5.31 @integration
  Scenario: See the project's requirements
    When I open the "Security policy" sub-tab of "Acme Portal"
    Then the table lists "SP-1" and "SP-2"

  @US-019 @integration
  Scenario: Search requirements
    When I open the "Security policy" sub-tab of "Acme Portal"
    And I search the requirements for "backup"
    Then the table lists only "SP-2"

  @US-019 @iso27001-5.31 @integration
  Scenario: The list follows the project's version
    Given version 2 of "Security policy" has the requirement "SP-3 Logging"
    When I move "Acme Portal" to the newest version of "Security policy"
    And I open the "Security policy" sub-tab of "Acme Portal"
    Then the table lists only "SP-3"

  @US-021 @iso27001-9.1 @integration
  Scenario: Link a user story to a requirement
    When I open the "Security policy" sub-tab of "Acme Portal"
    And I link "[US-004] Sign in with OpenID Connect" to the requirement "SP-1"
    Then the row "SP-1" is "Covered" by "[US-004] Sign in with OpenID Connect"
    And the row "SP-2" is "Not covered"
    And the coverage says "1 of 2 requirements covered (50 %)"

  @US-021 @integration
  Scenario: User stories show their requirements
    Given the user story "[US-004] Sign in with OpenID Connect" is linked to the requirement "SP-1"
    When I open the "User stories" tab of "Acme Portal"
    Then the user story "[US-004] Sign in with OpenID Connect" shows the requirements "SP-1"

  @US-021 @integration
  Scenario: Only what is not covered
    Given the user story "[US-004] Sign in with OpenID Connect" is linked to the requirement "SP-1"
    When I open the "Security policy" sub-tab of "Acme Portal"
    And I show only "Not covered"
    Then the table lists only "SP-2"

  @US-021 @integration
  Scenario: Removing a link keeps it as history
    Given the user story "[US-004] Sign in with OpenID Connect" is linked to the requirement "SP-1"
    When I open the "Security policy" sub-tab of "Acme Portal"
    And I remove the link between "[US-004] Sign in with OpenID Connect" and "SP-1"
    Then the row "SP-1" is "Not covered"
    And the removed link between "[US-004] Sign in with OpenID Connect" and "SP-1" is still stored

  @US-021 @iso27001-A.8.15 @integration
  Scenario: Linking is logged
    Given the user story "[US-004] Sign in with OpenID Connect" is linked to the requirement "SP-1"
    Then the activity log for "Acme ApS" has "RequirementLinked" for "Anna" concerning "Acme Portal"
    And the "RequirementLinked" entry says "SP-1 ↔ [US-004] Sign in with OpenID Connect"

  @US-041 @iso27001-5.31 @integration
  Scenario: One sub-tab per regulation
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the "Compliance" tab of "Acme Portal"
    Then I see the sub-tabs "CRA" and "Security policy"

  @US-041 @integration
  Scenario: Each sub-tab has its own address
    When I open the address "/Projects/Details/{id}/compliance/security-policy" of "Acme Portal"
    Then the "Security policy" sub-tab is active

  @US-041 @integration
  Scenario: An unknown sub-tab is not found
    When I open the address "/Projects/Details/{id}/compliance/nonsense" of "Acme Portal"
    Then the page is not found

  @US-041 @integration
  Scenario: A framework without requirements says so
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then I see "This framework has no requirements in the tracker yet"

  @US-041 @integration
  Scenario: The overview links to each regulation's coverage
    Given the user story "[US-004] Sign in with OpenID Connect" is linked to the requirement "SP-1"
    When I open the "Overview" tab of "Acme Portal"
    Then the coverage summary shows "Security policy: 1 of 2 covered"

  @US-041 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see it
    Given "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the address "/Projects/Details/{id}/compliance/security-policy" of "Acme Portal"
    Then the page is not found
