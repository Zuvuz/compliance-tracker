Feature: Session timeout

  # US-088: a session ends after 30 minutes without a request, and after 8 hours at most. These scenarios use the
  # app's real sign-in cookie (not the test sign-in) and the test clock, so time can pass between requests.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And the time is "04-11-2026 10:00"
    And "Anna" has signed in with the sign-in cookie

  @US-088 @asvs-V3.3 @iso27001-A.8.5 @integration
  Scenario: An active session goes on
    When the time is "04-11-2026 10:25"
    And she opens "/Projects"
    Then she sees the page
    When the time is "04-11-2026 10:50"
    And she opens "/Projects"
    Then she sees the page

  @US-088 @asvs-V3.3 @integration
  Scenario: An idle session ends
    When the time is "04-11-2026 10:31"
    And she opens "/Projects"
    Then she is sent to sign in, coming back to "/Projects"
    And the activity log for "Acme ApS" has "UserSignedOut" with "Timed out after 30 minutes without activity"

  @US-088 @asvs-V3.3 @integration
  Scenario: A session ends after the absolute time, also with activity
    When she opens a page every 20 minutes until "04-11-2026 18:00"
    And the time is "04-11-2026 18:01"
    And she opens "/Projects"
    Then she is sent to sign in, coming back to "/Projects"
    And the activity log for "Acme ApS" has "UserSignedOut" with "Session ended after 8 hours"

  @US-088 @asvs-V3.4 @integration
  Scenario: The sign-in cookie is HttpOnly, Secure and SameSite
    Then the sign-in cookie is "httponly", "secure" and "samesite=lax"
