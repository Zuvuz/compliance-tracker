Feature: Who knew what, and when

  # US-219: when the organisation admin has turned the access log on (at least 6 weeks ahead, so the members are told
  # first), opening a project page, a risk, evidence, a report or the suppliers is a visit: who, in which role, what and
  # when, and the time the page was visible. Only Organisation admin, Compliance manager and Auditor see the access log;
  # everyone who can read a project keeps its change log. Visits are deleted after the retention period.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the project "Acme Shop" exists
    And I add the risk "Data leak" to "Acme Portal" with likelihood 3 and impact 4
    And "Bo" has joined "Acme ApS" as "ComplianceManager"
    And "Carl" has joined "Acme ApS" as "Developer"
    And I am signed in as "Anna"

  @US-219 @iso27001-a8.15 @integration
  Scenario: Opening a risk is logged with the time spent
    Given the access log is on since "2026-11-01"
    And "Bo" opened the risk "Data leak" at "2026-11-20T10:00:00Z" and was last seen at "2026-11-20T10:08:00Z"
    When "Anna" opens the access log of "Acme Portal"
    Then the access log shows "Bo" opened "Risk: Data leak (Acme Portal)" at "2026-11-20 10:00" for "8 minutes"

  @US-219 @iso27001-a8.15 @integration
  Scenario: Opening a risk starts a visit, and the page's signs of life count the time
    Given the access log is on since "2026-11-01"
    And the time is "2026-11-27T09:00:00Z"
    When I open the risk "Data leak" of "Acme Portal"
    And the time is "2026-11-27T09:00:30Z"
    And the page sends a sign of life
    Then "Anna" has a visit to "Risk: Data leak (Acme Portal)" of 30 seconds

  @US-219 @gdpr-art5 @integration
  Scenario: A Developer sees only the change log
    Given the access log is on since "2026-11-01"
    And I am signed in as "Carl"
    When I open the activity of "Acme Portal"
    Then the log pages offer "Changes" only
    And "Carl" may not open the access log

  @US-219 @iso27001-a8.16 @integration
  Scenario: Who is on the page now
    Given the access log is on since "2026-11-01"
    And the time is "2026-11-27T09:00:00Z"
    And "Bo" has the risks of "Acme Portal" open with a sign of life 1 minute ago
    When I open the risks of "Acme Portal"
    Then I see "Bo" on the page now

  @US-219 @iso27001-a8.15 @integration
  Scenario: The access log can be filtered by project
    Given the access log is on since "2026-11-01"
    And "Bo" opened the risk "Data leak" at "2026-11-20T10:00:00Z" and was last seen at "2026-11-20T10:02:00Z"
    And "Bo" opened the risks of "Acme Shop" at "2026-11-21T10:00:00Z"
    When "Anna" opens the access log of "Acme Shop"
    Then the access log shows only "Acme Shop"

  @US-219 @gdpr-art5 @integration
  Scenario: Old visits are deleted, and the deletion is counted in the change log
    Given the access log is on since "2025-10-01"
    And "Bo" opened the risk "Data leak" at "2025-11-01T10:00:00Z" and was last seen at "2025-11-01T10:05:00Z"
    And "Bo" opened the risk "Data leak" at "2026-10-01T10:00:00Z" and was last seen at "2026-10-01T10:05:00Z"
    When the tracker cleans up on "2026-11-02T03:00:00Z"
    Then the access log has no visit from "2025-11-01"
    And the access log has a visit from "2026-10-01"
    And the change log says the system deleted 1 visit older than 12 months

  @US-219 @iso27001-a5.28 @integration
  Scenario: The audit package has the access log for its period
    Given the access log is on since "2026-01-01"
    And "Bo" opened the risk "Data leak" at "2026-03-02T10:00:00Z" and was last seen at "2026-03-02T10:04:00Z"
    When I create the audit package of "Acme Portal" from "2026-01-01" to "2026-06-30"
    Then the audit package's file "05-access-log-2026-01-01-to-2026-06-30.csv" says "Risk: Data leak (Acme Portal)"
    And the audit package's file "05-access-log-2026-01-01-to-2026-06-30.csv" says "Bo"

  @US-219 @gdpr-art88 @integration
  Scenario: The access log starts at least 6 weeks after it is turned on, and the members are told
    Given the time is "2026-11-27T09:00:00Z"
    When I turn on the access log from "2026-12-15" kept for 12 months
    Then the settings say "The access log can start on 08-01-2027 at the earliest, so the members are told 6 weeks before"
    When I turn on the access log from "2027-01-10" kept for 12 months
    Then the access log starts on "10-01-2027"
    And "Carl" is told on Home that the access log starts on "10-01-2027"
