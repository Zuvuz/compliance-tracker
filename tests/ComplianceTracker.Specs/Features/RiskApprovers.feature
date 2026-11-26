Feature: Chosen roles approve and accept High and Critical risks

  # US-217: the organisation chooses the roles that approve accepting a High or Critical risk; Organisation admin by
  # default. A member with one of them accepts at once; others who may decide compliance ask, and only a member with an
  # approver role can approve. The other four-eyes actions are unchanged.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "Bo" is in "Acme ApS" with the role "ComplianceManager"
    And "Carl" is in "Acme ApS" with the role "ComplianceManager"
    And the risk "Data leak" exists in "Acme Portal"

  @US-217 @iso27001-6.1.3 @integration
  Scenario: An approver role accepts a High risk at once
    When I ask to accept the risk "Data leak" with the reason "Insurance covers it"
    Then the risk "Data leak" has the status "Accepted"
    And nothing waits for approval in "Acme Portal"
    And the activity log has "RiskUpdated" saying "as Organisation admin (approver role, no approval needed; risk owner Anna)"

  @US-217 @iso27001-a5.3 @integration
  Scenario: A Compliance manager must ask an approver
    Given I am signed in as "Bo"
    When I ask to accept the risk "Data leak" with the reason "Insurance covers it"
    Then I see that it waits for another member's approval
    And "Carl" cannot approve the request and is told "Only Organisation admin can approve this"
    When "Anna" approves the request
    Then the risk "Data leak" has the status "Accepted"

  @US-217 @iso27001-5.3 @integration
  Scenario: The organisation admin chooses the approver roles
    When I let "Organisation admin and Compliance manager" approve accepting High and Critical risks
    Then the activity log has "RiskApproverRolesChanged" saying "From Organisation admin to Organisation admin, Compliance manager"
    Given I am signed in as "Bo"
    When I ask to accept the risk "Data leak" with the reason "Insurance covers it"
    Then the risk "Data leak" has the status "Accepted"

  @US-217 @iso27001-5.3 @integration
  Scenario: Only roles that may decide compliance can approve
    When I try to let "Developer" approve accepting High and Critical risks
    Then the settings page says "Developer may not decide compliance, so it cannot approve risks"

  @US-217 @iso27001-6.1.3 @integration
  Scenario: A Medium risk is accepted as before
    Given I am signed in as "Bo"
    When I add the risk "Old TLS" to "Acme Portal" with likelihood 3 and impact 3
    And I ask to accept the risk "Old TLS" with the reason "Retired in Q1"
    Then the risk "Old TLS" has the status "Accepted"
