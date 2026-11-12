Feature: Limits on heavy actions

  # US-097: AI actions are limited per organisation (10 a minute), uploads per user (20 a minute) and other changes per
  # user (120 a minute). A hit limit answers "Too many requests. Try again in a minute" (429), changes nothing, and is
  # logged as a security event (US-098). Pages that only show something are never limited.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-097 @asvs-V2.4.1 @integration
  Scenario: The AI can be asked 10 times a minute per organisation
    When I ask the AI about "I.2(d)" 11 times
    Then the first 10 answers were fine
    And the last answer says "Too many requests. Try again in a minute"
    And the log has the security event "request limit hit for POST /Projects/AskTheAi"

  @US-097 @asvs-V2.4.1 @integration
  Scenario: Uploads are limited to 20 a minute, and a refused one changes nothing
    When I upload 21 scans to "Acme Portal"
    Then the first 20 answers were fine
    And the last answer says "Too many requests. Try again in a minute"
    And "Acme Portal" has 20 scans

  @US-097 @integration
  Scenario: Pages that only show something are never limited
    When I open the projects page 150 times
    Then every answer was fine
