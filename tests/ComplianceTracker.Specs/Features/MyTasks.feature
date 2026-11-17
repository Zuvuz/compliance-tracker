Feature: My tasks across projects

  # US-099: "My tasks" in the top bar lists what has a deadline across the member's projects (expiring evidence, risk
  # reviews, risks they own, reporting deadlines, their audit findings), sorted by due date and filtered by project and
  # kind, above what waits for their decision (US-169). The top bar counts what is overdue or due within 7 days.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme App" exists
    And the time is "2025-11-01T09:00:00Z"
    And the risk "Stolen session cookie" exists in "Acme App"
    And I add the evidence "Pen test report" with the link "https://example.com/pentest" valid until "20-10-2026" to "I.2(d)"

  @US-099 @iso27001-9.1 @integration
  Scenario: Tasks from two projects are listed together, earliest first
    Given the time is "2026-11-02T09:00:00Z"
    When I open My tasks from the top bar
    Then "My tasks" lists 'Evidence "Pen test report" has expired' for "Acme Portal"
    And "My tasks" lists 'Review the risk "Stolen session cookie"' for "Acme App"
    And the tasks are sorted by due date
    And each task links to where it is handled
    And the top bar counts 2 tasks

  @US-099 @integration
  Scenario: The tasks can be filtered by kind and by project
    Given the time is "2026-11-02T09:00:00Z"
    When I open My tasks from the top bar
    And I show only the kind "Risk reviews"
    Then "My tasks" lists only 'Review the risk "Stolen session cookie"'
    When I show only the project "Acme Portal"
    Then "My tasks" lists only 'Evidence "Pen test report" has expired'

  @US-099 @integration
  Scenario: Nothing is due yet
    When I open My tasks from the top bar
    Then "My tasks" lists 'Your risk "Stolen session cookie"' for "Acme App"
    And the top bar counts 0 tasks

  @US-099 @iso27001-A.5.15 @integration
  Scenario: The auditor sees the tasks without links
    Given "Carl" is in "Acme ApS" with the role "Auditor"
    And the time is "2026-11-02T09:00:00Z"
    And I am signed in as "Carl"
    When I open My tasks from the top bar
    Then "My tasks" lists 'Review the risk "Stolen session cookie"' for "Acme App"
    And no task has a link
