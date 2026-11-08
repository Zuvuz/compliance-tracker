Feature: Confirmation and four-eyes approval

  # US-135: important actions ask for confirmation, and the most important ones need a second person's approval before
  # they happen: accepting a High or Critical risk, recording a security event as not reportable, and end of support.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "Bo" is in "Acme ApS" with the role "ComplianceManager"

  @US-135 @iso27001-A.5.3 @integration
  Scenario: A high risk needs a second person
    Given the risk "Stolen sessions" exists in "Acme Portal"
    When I ask to accept the risk "Stolen sessions" with the reason "Short-lived tokens"
    Then I see that it waits for another member's approval
    And the risk "Stolen sessions" has the status "Open"
    And "Bo" sees 'Waiting for your approval: Accept the risk "Stolen sessions" (High)' under "Needs attention" of "Acme Portal"
    When "Bo" approves the request
    Then the risk "Stolen sessions" has the status "Accepted"
    And the risk "Stolen sessions" says "Accepted by Anna (Organisation admin): Short-lived tokens"
    And the activity log for "Acme ApS" has "ApprovalGiven" for "Bo" concerning "Acme Portal"

  @US-135 @iso27001-6.1.3 @integration
  Scenario: A medium risk is accepted at once
    When I add the risk "Old TLS" to "Acme Portal" with likelihood 3 and impact 3
    And I ask to accept the risk "Old TLS" with the reason "Retired in Q1"
    Then the risk "Old TLS" has the status "Accepted"

  @US-135 @iso27001-A.5.3 @asvs-V2.3 @integration
  Scenario: You cannot approve your own request
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I ask to accept the risk "Stolen sessions" with the reason "Short-lived tokens"
    When I approve my own request
    Then I see the error "Another person must approve this"
    And the risk "Stolen sessions" has the status "Open"

  @US-135 @iso27001-A.5.3 @integration
  Scenario: A rejected request changes nothing
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I ask to accept the risk "Stolen sessions" with the reason "Short-lived tokens"
    When "Bo" rejects the request because "Tokens live for 30 days"
    Then the request has the status "Rejected"
    And the risk "Stolen sessions" has the status "Open"

  @US-135 @iso27001-A.8.32 @integration
  Scenario: A request is cancelled when its subject changed
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I ask to accept the risk "Stolen sessions" with the reason "Short-lived tokens"
    And I mark the risk "Stolen sessions" as reviewed
    When "Bo" approves the request
    Then I see the error "The request was cancelled: the risk changed after the request was made. Ask again if it is still needed."
    And the request has the status "Cancelled"
    And the risk "Stolen sessions" has the status "Open"

  @US-135 @integration
  Scenario: A request expires after 7 days
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I ask to accept the risk "Stolen sessions" with the reason "Short-lived tokens"
    And the request is 7 days old
    When "Bo" sends an approval of the request
    Then I see the error "The request expired after 7 days; ask again"
    And the request has the status "Expired"

  @US-135 @integration
  Scenario: The person who asked can withdraw the request
    Given the risk "Stolen sessions" exists in "Acme Portal"
    And I ask to accept the risk "Stolen sessions" with the reason "Short-lived tokens"
    When I withdraw the request
    Then the request has the status "Cancelled"

  @US-135 @cra-art14 @iso27001-A.5.3 @integration
  Scenario: A security event that is not reported needs a second person
    When I ask to record the vulnerability "XSS in search", not exploited, for "Acme Portal"
    Then I see that it waits for another member's approval
    And 'Record "XSS in search" as vulnerability, not exploited' of "Acme Portal" waits for approval
    When "Bo" approves the request
    Then the event "XSS in search" needs no reports

  @US-135 @cra-art13 @iso27001-A.5.3 @integration
  Scenario: End of support needs a second person
    Given "Acme Portal" is in phase "Released"
    When I ask to end the support of "Acme Portal"
    Then I see that it waits for another member's approval
    And the page of "Acme Portal" shows phase "Released"
    When "Bo" approves the request
    Then the page of "Acme Portal" shows phase "EndOfSupport"

  @US-135 @iso27001-A.5.3 @integration
  Scenario: A member whose role does not allow the action cannot approve it
    Given "Dan" is in "Acme ApS" with the role "Developer"
    And the risk "Stolen sessions" exists in "Acme Portal"
    And I ask to accept the risk "Stolen sessions" with the reason "Short-lived tokens"
    Then "Dan" cannot approve the request

  @US-135 @asvs-V2.3 @integration
  Scenario Outline: Important actions ask for confirmation first
    Given the risk "Stolen sessions" exists in "Acme Portal"
    Then the "<form>" form on "<page>" asks for confirmation
    And the page has the confirmation dialog

    Examples:
      | form          | page                             |
      | #accept-risk  | the risk page of Stolen sessions |
      | #close-risk   | the risk page of Stolen sessions |
      | #change-phase | the Overview of Acme Portal      |
