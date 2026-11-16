Feature: The audit package locks its reports and compares with the last package

  # US-198: making an audit package also locks each report in it, listed under the package, and "Since last" in the
  # package's management report compares with the reports of the previous package by itself.

  Background:
    Given an empty database
    And the time is "2026-11-12T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"

  @US-198 @iso27001-A.5.33 @integration
  Scenario: The first package locks its reports and has nothing to compare with
    When I create the audit package of "Acme Portal"
    Then the newest audit package has 3 locked reports under it on the Reports tab
    And the package's management report says "First package: nothing to compare with"

  @US-198 @iso27001-9.3 @integration
  Scenario: The second package compares with the first
    Given I have created the audit package of "Acme Portal"
    And the time is "2026-11-20T09:00:00Z"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    When I create the audit package of "Acme Portal"
    Then the package's management report says "since 12-11-2026"
    And the package's management report does not say "First package"

  @US-198 @iso27001-9.3 @integration
  Scenario: A framework added since the last package is new
    Given I have created the audit package of "Acme Portal"
    And "AI Act – Regulation (EU) 2024/1689" is switched on
    And "Acme Portal" uses "AI Act – Regulation (EU) 2024/1689"
    And the time is "2026-11-20T09:00:00Z"
    When I create the audit package of "Acme Portal"
    Then the package's management report says "New"
