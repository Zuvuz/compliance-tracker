Feature: Invite by e-mail, with a time limit for consultants and auditors

  # US-196: an organisation admin invites a person from the Users page by e-mail and role; for a consultant or an
  # auditor also an end date, and for an auditor one project. The link is mailed at once and shown once.

  Background:
    Given an empty database
    And the time is "2026-11-16T09:00:00Z"
    And "Anna" has an organisation "Acme ApS" with the projects "Acme Portal" and "Acme Shop"

  @US-196 @iso27001-A.5.18 @integration
  Scenario: A consultant's invitation is mailed and their access ends by itself
    When "Anna" invites "carl@example.com" as "Consultant" until "2026-12-31"
    Then a mail with the invitation link is sent to "carl@example.com"
    And "Anna" sees the open invitation to "carl@example.com" until "31-12-2026"
    When "Carl" accepts the mailed invitation
    Then "Carl" can open the project "Acme Portal"
    And the member "Carl" has access until "31-12-2026"
    When the time is "2027-01-01T09:00:00Z"
    Then "Carl" cannot open the project "Acme Portal"

  @US-196 @iso27001-A.5.18 @integration
  Scenario: An auditor limited to one project sees only that project
    When "Anna" invites "dora@example.com" as "Auditor" until "2026-12-15" for the project "Acme Portal"
    And "Dora" accepts the mailed invitation
    Then "Dora" can open the project "Acme Portal"
    And "Dora" cannot open the project "Acme Shop"

  @US-196 @iso27001-A.5.16 @integration
  Scenario: The invitation can only be used by the person it was sent to
    When "Anna" invites "carl@example.com" as "Consultant" until "2026-12-31"
    And "Erik" opens the mailed invitation
    Then "Erik" is told "This invitation is for carl@example.com"

  @US-196 @iso27001-A.5.18 @integration
  Scenario: The end date is changed on the Users page and logged
    Given "Anna" has invited "carl@example.com" as "Consultant" until "2026-12-31" and "Carl" has accepted
    When "Anna" sets the end date of "Carl" to "2027-01-31"
    Then the member "Carl" has access until "31-01-2027"
    And the activity log has "MemberAccessChanged" saying "Access until: 31-12-2026 → 31-01-2027"

  @US-196 @integration
  Scenario: Without a mail server the link is shown to the admin
    Given the mail server is not set up
    When "Anna" invites "carl@example.com" as "Consultant" until "2026-12-31"
    Then no mail is sent
    And "Anna" is shown the invitation link

  @US-196 @integration
  Scenario: An end date in the past is refused
    When "Anna" invites "carl@example.com" as "Consultant" until "2026-11-01"
    Then "Anna" is told "The end date must be today or later"
