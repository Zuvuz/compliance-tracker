Feature: Treatment actions for risks
  US-111: each risk can have actions with an owner and a due date, linked to the user stories and evidence that carry
  them out, so a risk can be traced from the assessment through the action to the proof (ISO 27001 6.1.3, 8.3; CRA Art. 13(2)).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the project "Acme Portal" has the user story "#41 Session timeout"
    And the risk "Stolen sessions" exists in "Acme Portal"

  @US-111 @iso27001-6.1.3 @integration
  Scenario: Add an action and link it to a story
    When I add the action "Short session lifetime" to the risk "Stolen sessions" due "01-12-2026"
    And I link the action "Short session lifetime" of "Stolen sessions" to the story "#41 Session timeout"
    Then the action "Short session lifetime" of "Stolen sessions" is "In progress"
    And the action "Short session lifetime" of "Stolen sessions" traces to the story "#41 Session timeout"

  @US-111 @iso27001-6.1.3 @integration
  Scenario: An action is done only with proof
    Given I have added the action "Short session lifetime" to the risk "Stolen sessions"
    When I mark the action "Short session lifetime" of "Stolen sessions" as done
    Then I see the error "Link a done work item or evidence first"

  @US-111 @iso27001-8.3 @integration
  Scenario: A closed story completes the action
    Given I have added the action "Short session lifetime" to the risk "Stolen sessions"
    And I have linked the action "Short session lifetime" of "Stolen sessions" to the story "#41 Session timeout"
    And the user story "#41 Session timeout" is closed
    When I mark the action "Short session lifetime" of "Stolen sessions" as done
    Then the action "Short session lifetime" of "Stolen sessions" is "Done"
    And the risk action log of "Acme Portal" has "Stolen sessions: Short session lifetime"

  @US-111 @iso27001-7.5.3 @integration
  Scenario: Evidence is proof too
    Given I upload the evidence "Session config" with the file "session.pdf" on the Evidence tab of "Acme Portal"
    And I have added the action "Short session lifetime" to the risk "Stolen sessions"
    And I have linked the action "Short session lifetime" of "Stolen sessions" to the evidence "Session config"
    When I mark the action "Short session lifetime" of "Stolen sessions" as done
    Then the action "Short session lifetime" of "Stolen sessions" is "Done"

  @US-111 @integration
  Scenario: Overdue actions need attention
    Given I have added the action "Short session lifetime" to the risk "Stolen sessions" due "01-01-2026"
    When "Anna" opens the "Risks" tab of "Acme Portal"
    Then the risk "Stolen sessions" is marked "1 overdue action"
    When "Anna" opens the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "1 overdue risk action"
