Feature: Home, the page after sign-in

  # US-227: sign-in lands on Home. Home has one part, "Your week": the member's tasks with a deadline (US-099), the ISMS
  # overview's "My week" (US-216) and the approvals that wait for the member (US-135), as one list, overdue first, up to
  # 7 days ahead; the rest is under "Show more". The ISMS status and the guided start stay on the ISMS overview.

  Background:
    Given an empty database
    And the time is "2026-11-11T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "Bo" has joined "Acme ApS" as "ComplianceManager"

  @US-227 @usability @integration
  Scenario: Sign-in lands on Home
    When I open the front page
    Then I am on Home
    And the top bar marks "Home"

  @US-227 @iso27001-8.1 @integration
  Scenario: My own items come first, overdue first
    Given "Bo" owns the quarterly task "Access review" with the deadline "2026-11-15"
    And I upload the policy "Access control policy" version "4" for the organisation
    And the time is "2026-11-21T09:00:00Z"
    And I am signed in as "Bo"
    When I open Home
    Then the first item on Home is "Review access rights" marked overdue
    And Home lists "Review access rights" before "Read the Access control policy"

  @US-227 @iso27001-8.1 @integration
  Scenario: What is due later is under "Show more"
    Given "Bo" owns the quarterly task "Access review" with the deadline "2026-12-15"
    And I am signed in as "Bo"
    When I open Home
    Then Home lists "Review access rights" under "Show more"
    And Home has nothing for this week

  @US-227 @iso27001-a5.3 @integration
  Scenario: An approval that waits for me is on Home
    Given the project "Acme Portal" exists
    And the risk "Data leak" exists in "Acme Portal"
    And I am signed in as "Bo"
    And I ask to accept the risk "Data leak" with the reason "Insurance covers it"
    And I am signed in as "Anna"
    When I open Home
    Then Home lists an approval about "Data leak" that opens the approvals of "Acme Portal"
    And "Bo" has no approvals on Home, since the request waits for someone else
