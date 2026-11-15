Feature: Reminders by e-mail before deadlines

  # US-183: the tracker sends an e-mail before a deadline is missed: evidence that expires, audit findings that are due,
  # approvals that wait, the CRA/NIS2 reporting deadlines and supplier evidence. One mail per deadline; a mail holds no
  # confidential text; a mail that fails is tried again; the reporting deadlines cannot be turned off.

  Background:
    Given an empty database
    And the time is "2026-11-01T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-183 @iso27001-9.1 @integration
  Scenario: Evidence that expires gives one reminder
    Given I add the evidence "Access policy" with the link "https://example.com/policy" valid until "01-12-2026" to "I.2(d)"
    When the reminders run
    And the reminders run
    Then "Anna" has received 1 mail about "evidence expires on 01-12-2026"
    And the mail to "Anna" does not contain "Access policy"

  @US-183 @integration
  Scenario: Evidence that expires later gives no reminder yet
    Given I add the evidence "Access policy" with the link "https://example.com/policy" valid until "01-06-2027" to "I.2(d)"
    When the reminders run
    Then "Anna" has received 0 mails about "evidence expires"

  @US-183 @iso27001-10.1 @integration
  Scenario: An audit finding that is due reminds its owner
    Given an audit of "Acme Portal" found "Access reviews not documented" for "Anna" due "2026-11-20"
    When the reminders run
    Then "Anna" has received 1 mail about "an audit finding is due on 20-11-2026"

  @US-183 @cra-art14 @integration
  Scenario: A reporting deadline cannot be turned off
    Given I have turned off every reminder I can
    And I record the actively exploited vulnerability "CVE-2026-1234" for "Acme Portal", aware since "2026-11-01T08:00:00Z"
    When the reminders run
    Then "Anna" has received 2 mails about "a security report is due"
    And "Anna" has received 0 mails about "audit finding"

  @US-183 @iso27001-9.1 @integration
  Scenario: A mail that fails is tried again
    Given I add the evidence "Access policy" with the link "https://example.com/policy" valid until "01-12-2026" to "I.2(d)"
    And the mail server is down
    And the reminders have run
    When the mail server is up again
    And the reminders run
    Then "Anna" has received 1 mail about "evidence expires on 01-12-2026"

  @US-183 @integration
  Scenario: Without a mail server nothing is sent and the page says so
    Given no mail server is set up
    And I add the evidence "Access policy" with the link "https://example.com/policy" valid until "01-12-2026" to "I.2(d)"
    When the reminders run
    Then "Anna" has received 0 mails about "evidence expires"
    And the Reminders page says that no mail server is set up

  @US-183 @integration
  Scenario: The organisation admin sets the days before
    Given I add the evidence "Access policy" with the link "https://example.com/policy" valid until "01-12-2026" to "I.2(d)"
    And I set the reminders to 10 days before
    When the reminders run
    Then "Anna" has received 0 mails about "evidence expires"
