Feature: The project overview shows the status and what needs attention

  # US-048: the Overview tab shows coverage per framework, what needs attention, open risks per level,
  # the phase and the latest activity. The phase checklist (US-047) comes when that story is built.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-048 @iso27001-9.1 @integration
  Scenario: Coverage per framework
    Given the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"
    And the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    When I open the "Overview" tab of "Acme Portal"
    Then the coverage of "CRA" is "5 %"

  @US-048 @iso27001-9.1 @integration
  Scenario: What is not covered needs attention and links to it
    When I open the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "22 requirements not covered"
    And "22 requirements not covered" links to the "CRA" sub-tab showing "Not covered"

  @US-048 @cra-art13 @integration
  Scenario: Risks due for review need attention
    Given the time is "2025-11-01T09:00:00Z"
    And the risk "Stolen session cookie" exists in "Acme Portal"
    And I mark the risk "Stolen session cookie" as reviewed
    When the time is "2026-11-02T09:00:00Z"
    And I open the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "1 risk due for review"

  @US-048 @integration
  Scenario: The risk summary counts open risks per level
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    When I open the "Overview" tab of "Acme Portal"
    Then the risk summary shows 1 "High"

  @US-048 @iso27001-9.3 @integration
  Scenario: The latest activity is shown
    Given the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"
    And the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    When I open the "Overview" tab of "Acme Portal"
    Then the latest activity starts with "Anna (Organisation admin)"

  @US-048 @integration
  Scenario: Nothing needs attention
    Given the project "Beta App" exists
    When I open the "Overview" tab of "Beta App"
    Then "Needs attention" says "Nothing needs attention"

  @US-048 @iso27001-A.5.15 @integration
  Scenario: An auditor sees the overview without the phase form
    Given "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "22 requirements not covered"
    And there is no "Change phase" button
