Feature: The thread from risk to requirement
  US-124: a risk shows its whole thread: measures and actions, the user stories that carry them out, the requirements
  those stories cover, and the evidence. A requirement shows the open risks that reach it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "#41 Session timeout"
    And the user story "#41 Session timeout" is linked to the requirement "I.2(d)"
    And I have added the evidence "Session config" with the link "https://example.com/session" to "I.2(d)"
    And the risk "Stolen sessions" exists in "Acme Portal"
    And I have added the action "Short session lifetime" to the risk "Stolen sessions"
    And I have linked the action "Short session lifetime" of "Stolen sessions" to the story "#41 Session timeout"

  @US-124 @iso27001-6.1.3 @cra-annexVII @integration
  Scenario: The risk shows its whole thread
    When I open the trace of the risk "Stolen sessions"
    Then the trace goes from the action "Short session lifetime" to the story "#41 Session timeout"
    And the trace goes from the story "#41 Session timeout" to the requirement "I.2(d)"
    And the trace goes from the requirement "I.2(d)" to the evidence "Session config"

  @US-124 @iso27001-6.1.3 @integration
  Scenario: The requirement shows the risk behind it
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" names the risk "Stolen sessions"

  @US-124 @integration
  Scenario: A requirement that is a measure is reached directly
    Given the risk "Phishing" exists in "Acme Portal"
    And the risk "Phishing" has the requirement "I.2(e)" as a measure
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(e)" names the risk "Phishing"
    And the row "I.2(a)" names no risk

  @US-124 @integration
  Scenario: A closed risk is not shown on the requirement
    Given I close the risk "Stolen sessions" with the reason "The portal no longer has sessions"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" names no risk

  @US-124 @iso27001-9.2 @integration
  Scenario: The auditor report names the risks
    When I download the traceability matrix of "Acme Portal" as Excel for an auditor
    Then on "CRA" the row "I.2(d)" says "Risks" "Stolen sessions (High)" and "Decided by" "Anna"

  @US-124 @integration
  Scenario: A risk without measures or actions has nothing to trace
    Given the risk "Supplier outage" exists in "Acme Portal"
    When I open the trace of the risk "Supplier outage"
    Then the trace says "No measures or actions yet, so nothing to trace"
