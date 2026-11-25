Feature: Management review of the ISMS

  # US-210: top management reviews the ISMS (ISO/IEC 27001 9.3). The tracker gathers every input of 9.3.2 from the ISMS and
  # the projects inside the scope; when the review is held the inputs are kept as they were. Decisions can have actions, and
  # the locked minutes cannot be changed. The page is "Management review" in the ISMS area.

  Background:
    Given an empty database
    And the time is "2026-12-10T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Webshop" exists
    And I put "Webshop" inside the ISMS scope

  @US-210 @iso27001-9.3 @integration
  Scenario: The input pack has every input the standard asks for
    When I plan a management review on "2026-12-10" with "Anna"
    Then the input pack has the parts "Actions from the last review", "Changes in the context", "Objectives", "Nonconformities", "Audit results", "Risks"
    And the input "Audit results" says "Nothing to report"

  @US-210 @iso27001-9.3 @integration
  Scenario: The inputs are kept when the review is held
    Given I add the risk "Ransomware" to "Webshop" with likelihood 5 and impact 5
    And a management review was held on "2026-12-10"
    When I add the risk "Data leak" to "Webshop" with likelihood 4 and impact 4
    Then the input "Risks" of the held review lists "Ransomware (Critical, Webshop)" but not "Data leak"

  @US-210 @iso27001-9.3 @integration
  Scenario: The minutes are locked with the decisions
    Given a management review was held on "2026-12-10"
    When I record the decision "Hire a security engineer" with an action for "Anna" before "2027-03-01"
    And I lock the minutes
    Then the minutes cannot be changed
    And the action "Hire a security engineer" is open for "Anna"

  @US-210 @iso27001-9.3 @integration
  Scenario: An open action is an input to the next review
    Given a management review was held on "2026-12-10"
    And I record the decision "Hire a security engineer" with an action for "Anna" before "2027-03-01"
    When I plan a management review on "2027-06-10" with "Anna"
    Then the input "Actions from the last review" says "Hire a security engineer: Anna, due 01-03-2027"

  @US-210 @iso27001-9.3 @integration
  Scenario: A review is due when none has been held for a year
    When I open the management review
    Then the management review is due

  @US-210 @iso27001-9.3 @integration
  Scenario: A member who does not decide compliance reads the reviews
    Given a management review was held on "2026-12-10"
    And "Dan" has joined "Acme ApS" as "Developer"
    And I am signed in as "Dan"
    When I open the management review
    Then the management review is read-only
