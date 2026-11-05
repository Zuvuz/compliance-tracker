Feature: The organisation's activity log

  # US-102: one page, /Activity, with every log entry of the organisation, also those that are not about a project
  # (sign-ins, members, invitations, frameworks). Searched, filtered and sorted like a project's Activity tab (US-101).
  # Only the organisation's Admins and Auditors can open it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-102 @iso27001-A.8.15 @integration
  Scenario: The page shows entries that are not about a project
    Given I have created an invitation link with the role "Developer"
    When I open the organisation's activity
    Then I see an "InvitationCreated" entry
    And I see a "ProjectCreated" entry about "Acme Portal"

  @US-102 @iso27001-A.5.18 @integration
  Scenario: Filtering on entries not about a project
    Given I have created an invitation link with the role "Developer"
    When I filter the organisation's activity on "Not about a project"
    Then I see an "InvitationCreated" entry
    And I see no "ProjectCreated" entry

  @US-102 @integration
  Scenario: Filtering on one project
    Given I have created an invitation link with the role "Developer"
    When I filter the organisation's activity on "Acme Portal"
    Then I see a "ProjectCreated" entry about "Acme Portal"
    And I see no "InvitationCreated" entry

  @US-102 @integration
  Scenario: Searching across the organisation finds what an entry is about
    When I search the organisation's activity for "acme portal"
    Then I see a "ProjectCreated" entry about "Acme Portal"

  @US-102 @integration
  Scenario: A removed member is shown as removed
    Given "Carl" has joined "Acme ApS" as "Developer"
    And I have removed "Carl"
    When I open the organisation's activity
    Then I see a "MemberRemoved" entry about "Carl (removed)"

  @US-102 @iso27001-A.5.15 @integration
  Scenario: Only Admins and Auditors can open the page
    Given "Carl" has joined "Acme ApS" as "Developer"
    And "Ida" has joined "Acme ApS" as "Auditor"
    When "Carl" opens the organisation's activity
    Then access is denied
    When "Ida" opens the organisation's activity
    Then "Ida" sees the organisation's activity

  @US-102 @iso27001-A.5.15 @integration
  Scenario: Another organisation's entries are never shown
    Given "Bo" has created the organisation "Beta ApS"
    When "Bo" searches the organisation's activity for "Acme"
    Then "Bo" sees no entries from "Acme ApS"
