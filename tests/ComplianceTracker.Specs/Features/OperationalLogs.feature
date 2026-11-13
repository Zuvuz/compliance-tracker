Feature: Operational logs and monitoring

  # US-098: the log lines carry the request's reference and the user's id (never a name, an e-mail or a token);
  # refused access is a security event; the error page shows the same reference as the log.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-098 @iso27001-A.8.15 @asvs-V7.2.1 @integration
  Scenario: Refused access is logged as a security event
    Given "Carl" has joined "Acme ApS" as "Developer"
    When "Carl" opens the address "/Activity"
    Then the log has the security event "access refused (403) for GET /Activity"
    And that log line has a reference and the user id of "Carl", but not the name or e-mail

  @US-098 @asvs-V7.4.1 @integration
  Scenario: The error page shows a reference and no details
    When I open the error page
    Then the error page shows a reference
    And the error page says "Something went wrong"
