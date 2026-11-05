Feature: Roles, invitations and the system admin

  # US-009: the organisation admin invites colleagues with a link and a role.
  # US-010: four roles (Organisation admin, Compliance manager, Developer, Auditor), checked on the server.
  # A role change only changes what a member may do from then on; earlier decisions keep the role they had then.
  # The system admin (Keycloak realm role "system-admin") manages roles in every organisation, but cannot see their data.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-009 @iso27001-A.5.18 @integration
  Scenario: Invite a colleague with a role
    When I create an invitation link with the role "Developer"
    And "Bo" opens the invitation link and joins
    Then "Bo" is a member of "Acme ApS" with the role "Developer"

  @US-009 @integration
  Scenario: An invitation link works once
    Given I have created an invitation link with the role "Developer"
    And "Bo" has opened the invitation link and joined
    When "Carl" opens the invitation link
    Then the invitation says "The invitation has already been used"

  @US-009 @integration
  Scenario: An invitation link expires after 7 days
    Given the time is "2026-11-02T09:00:00Z"
    And I have created an invitation link with the role "Developer"
    When the time is "2026-11-09T09:00:01Z"
    And "Bo" opens the invitation link
    Then the invitation says "The invitation has expired"

  @US-009 @integration
  Scenario: A revoked invitation no longer works
    Given I have created an invitation link with the role "Developer"
    When I revoke the invitation
    And "Bo" opens the invitation link
    Then the invitation says "The invitation has been revoked"

  @US-009 @integration
  Scenario: A member of another organisation cannot join
    Given I have created an invitation link with the role "Developer"
    And "Bo" has created the organisation "Beta ApS"
    When "Bo" opens the invitation link
    Then the invitation says "You are already a member of an organisation"

  @US-009 @iso27001-A.5.17 @integration
  Scenario: The link's token is not stored in plain text
    When I create an invitation link with the role "Developer"
    Then the database does not contain the invitation's token

  @US-010 @iso27001-A.5.18 @iso27001-A.8.15 @integration
  Scenario: The organisation admin changes a role, and it is logged
    Given "Bo" has joined "Acme ApS" as "Developer"
    When I change the role of "Bo" to "ComplianceManager"
    Then "Bo" is a member of "Acme ApS" with the role "ComplianceManager"
    And the "MemberRoleChanged" entry says "Bo: Developer → Compliance manager"

  @US-010 @integration
  Scenario: The organisation keeps at least one organisation admin
    When I change the role of "Anna" to "Developer"
    Then I see the error "The organisation must keep at least one organisation admin"

  @US-010 @iso27001-A.5.15 @integration
  Scenario: Permissions are checked on the server
    Given "Bo" has joined "Acme ApS" as "Auditor"
    And I am signed in as "Bo"
    When I create the project "Hidden" with intended purpose "Anything"
    Then I see the error "Your role does not allow this"

  @US-010 @integration
  Scenario: Only the organisation admin changes roles
    Given "Bo" has joined "Acme ApS" as "ComplianceManager"
    And I am signed in as "Bo"
    When I change the role of "Anna" to "Auditor"
    Then "Anna" is a member of "Acme ApS" with the role "Admin"

  @US-010 @integration
  Scenario: Buttons the role may not use are hidden
    Given "Bo" has joined "Acme ApS" as "Auditor"
    And I am signed in as "Bo"
    When I open the organisation page
    Then there is no "New project" button

  @US-010 @aiact-art14 @iso27001-A.8.15 @integration
  Scenario: A demoted member's earlier decisions keep the role they had then
    Given the project "Acme Portal" exists
    And the risk "Old TLS on legacy API" exists in "Acme Portal"
    And "Bo" has joined "Acme ApS" as "ComplianceManager"
    And I am signed in as "Bo"
    And I accept the risk "Old TLS on legacy API" with the reason "Retired in Q1"
    And I am signed in as "Anna"
    When I change the role of "Bo" to "Developer"
    Then the risk "Old TLS on legacy API" says "Accepted by Bo (Compliance manager): Retired in Q1"
    And the "RiskUpdated" log entry by "Bo" has the role "ComplianceManager"

  @US-010 @iso27001-A.5.18 @integration
  Scenario: The system admin changes a role in any organisation
    Given "Bo" has joined "Acme ApS" as "Developer"
    And "Sys" is the system admin
    When the system admin changes the role of "Bo" in "Acme ApS" to "Admin"
    Then "Bo" is a member of "Acme ApS" with the role "Admin"
    And the "MemberRoleChanged" entry says "System admin Sys: Bo: Developer → Organisation admin"

  @US-010 @iso27001-A.5.15 @integration
  Scenario: The system admin cannot see an organisation's data
    Given the project "Acme Portal" exists
    And "Sys" is the system admin
    When the system admin opens the page of "Acme Portal"
    Then the page is not found

  @US-010 @iso27001-A.5.15 @integration
  Scenario: Only the system admin opens the system pages
    When I open the system pages
    Then access is denied
