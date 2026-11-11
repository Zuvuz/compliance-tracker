Feature: One tab with the gaps the tracker finds

  # US-119: the Suggestions tab lists the project's gaps in four categories: Requirements, Security, Risk and Scanning.
  # The checks need no AI. A gap only becomes work when a person acts on it, and a dismissed gap stays away until its
  # source changes.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-119 @iso27001-10.2 @integration
  Scenario: Gaps are grouped by category
    Given the risk "Stolen sessions" exists in "Acme Portal"
    When I open the "Suggestions" tab of "Acme Portal" for gaps
    Then the category "Requirements" lists "I.2(d) "
    And the category "Risk" lists "Stolen sessions" because "No treatment actions (High)"

  @US-119 @cra-annexI @integration
  Scenario: A high scan finding is a gap, and the category says what it needs before
    When I open the "Suggestions" tab of "Acme Portal" for gaps
    Then the category "Scanning" says "Import scan results to find gaps here"
    Given "Acme Portal" has a "CodeQL" scan with the findings "SQL injection in /search" and "XSS in /profile"
    When I open the "Suggestions" tab of "Acme Portal" for gaps
    Then the category "Scanning" lists "SQL injection in /search"
    And the gap "SQL injection in /search" offers to add a risk

  @US-119 @aiact-art14 @integration
  Scenario: Dismissed gaps stay away
    When I dismiss the gap "I.2(i) " because "The product has no network connection"
    Then the category "Requirements" does not list "I.2(i) "
    And the activity log has "GapDismissed" saying "The product has no network connection"

  @US-119 @integration
  Scenario: A dismissed gap comes back when its source changes
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I dismiss the gap "Stolen sessions" because "Actions are planned elsewhere"
    When the risk "Stolen sessions" gets the action "Rotate session keys"
    And I open the "Suggestions" tab of "Acme Portal" for gaps
    Then the category "Risk" does not list "Stolen sessions"

  @US-119 @iso27001-A.5.15 @integration
  Scenario: An auditor sees the gaps but cannot act on them
    Given "Bo" is in "Acme ApS" with the role "Auditor"
    And I am signed in as "Bo"
    When I open the "Suggestions" tab of "Acme Portal" for gaps
    Then the category "Requirements" lists "I.2(d) "
    And no gap can be dismissed
