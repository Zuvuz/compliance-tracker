Feature: Time-limited read-only access for an external auditor

  # US-182: a member who may decide compliance gives an external auditor read-only access to one project for at most 30
  # days, through an invitation link. The auditor sees only that project, changes nothing, every page is logged, and the
  # access ends by itself or when it is revoked.

  Background:
    Given an empty database
    And the time is "2026-11-16T09:00:00Z"
    And "Anna" has an organisation "Acme ApS" with the projects "Acme Portal" and "Acme Shop"

  @US-182 @iso27001-A.5.15 @integration
  Scenario: The auditor reads the granted project and nothing else
    Given "Anna" has given "eve@audit.example" access to "Acme Portal" from "2026-11-16" to "2026-11-30"
    And "Eve" has accepted the auditor link
    Then "Eve" can open the project "Acme Portal"
    And "Eve" cannot open the project "Acme Shop"
    And "Eve" cannot open "/Settings"
    And "Eve" cannot open "/Users"

  @US-182 @iso27001-A.8.15 @integration
  Scenario: Every page the auditor opens is logged
    Given "Anna" has given "eve@audit.example" access to "Acme Portal" from "2026-11-16" to "2026-11-30"
    And "Eve" has accepted the auditor link
    When "Eve" can open the project "Acme Portal"
    Then the activity log has "AuditorAccessUsed" saying "Details"
    And the activity log has "AuditorAccessGranted" saying "eve@audit.example: 16-11-2026 – 30-11-2026"

  @US-182 @iso27001-A.5.18 @integration
  Scenario: The access ends by itself after the last day
    Given "Anna" has given "eve@audit.example" access to "Acme Portal" from "2026-11-16" to "2026-11-30"
    And "Eve" has accepted the auditor link
    When the time is "2026-12-01T09:00:00Z"
    Then "Eve" is told that the access to "Acme ApS" has ended

  @US-182 @iso27001-A.5.18 @integration
  Scenario: A revoked access ends at once
    Given "Anna" has given "eve@audit.example" access to "Acme Portal" from "2026-11-16" to "2026-11-30"
    And "Eve" has accepted the auditor link
    When "Anna" revokes the access of "eve@audit.example" to "Acme Portal"
    Then "Eve" cannot open the project "Acme Portal"
    And the activity log has "AuditorAccessRevoked" saying "eve@audit.example"

  @US-182 @integration
  Scenario: An access longer than 30 days is refused
    When "Anna" gives "eve@audit.example" access to "Acme Portal" from "2026-11-16" to "2026-12-31"
    Then "Anna" sees the error "The access can last at most 30 days"

  @US-182 @iso27001-A.5.15 @integration
  Scenario: A developer cannot give an auditor access
    Given "Carl" is in "Acme ApS" with the role "Developer"
    When "Carl" looks at the auditor access of "Acme Portal"
    Then there is no form to give an auditor access
