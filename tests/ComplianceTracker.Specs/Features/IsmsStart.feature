Feature: The ISMS start page

  # US-216: the first page of the ISMS area tells each member what they must do this week, shows how far the organisation
  # is with each part of the ISMS (clauses 4–10 and the SoA) with a plain sentence on what is missing, and guides a new
  # organisation through the first steps. Sign-in still lands on the projects (the user's choice).

  Background:
    Given an empty database
    And the time is "2026-11-11T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "Bo" has joined "Acme ApS" as "ComplianceManager"

  @US-216 @iso27001-4.4 @integration
  Scenario: My week shows my overdue task first
    Given "Bo" owns the quarterly task "Access review" with the deadline "2026-11-15"
    And I upload the policy "Access control policy" version "4" for the organisation
    And the time is "2026-11-21T09:00:00Z"
    And I am signed in as "Bo"
    When I open the ISMS start page
    Then "My week" shows "Review access rights" before "Read the Access control policy"
    And "Review access rights" is marked overdue

  @US-216 @iso27001-9.3 @integration
  Scenario: The ISMS status tells what is missing
    Given the time is "2025-09-01T09:00:00Z"
    And a management review was held on "2025-09-01"
    And the time is "2026-11-21T09:00:00Z"
    When I open the ISMS start page
    Then the ISMS part "Performance evaluation" is red with "No management review for 14 months"

  @US-216 @usability @integration
  Scenario: A new organisation is guided to the next step
    Given I have described the ISMS scope as "Our SaaS products"
    When I open the ISMS start page
    Then the guided start says "Next: add your policies"

  @US-216 @usability @integration
  Scenario: The guided start can be hidden and brought back
    Given I hide the guided start
    When I open the ISMS start page
    Then there is no guided start
    And I can show the guided start again

  @US-216 @usability @integration
  Scenario: A member who may only read sees the status but no buttons
    Given "Dan" has joined "Acme ApS" as "Developer"
    And I am signed in as "Dan"
    When I open the ISMS start page
    Then the ISMS part "Operation" is red with "No tasks in the yearly plan"
    And the ISMS start page has no buttons to change anything
