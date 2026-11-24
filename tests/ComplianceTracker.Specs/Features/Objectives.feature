Feature: Information security objectives and how they are measured

  # US-209: the organisation sets information security objectives with a target (ISO/IEC 27001 6.2) and measures them
  # (9.1). The tracker measures its own figures every hour over the projects inside the ISMS scope (US-208); the others are
  # measured by hand. The page is "Objectives" in the ISMS area.

  Background:
    Given an empty database
    And the time is "2026-11-24T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Webshop" exists
    And the project "Old intranet" exists
    And I put "Webshop" inside the ISMS scope

  @US-209 @iso27001-9.1 @integration
  Scenario: The tracker measures an objective by itself
    Given I add the risk "Data leak" to "Webshop" with likelihood 4 and impact 4
    And I add the risk "Ransomware" to "Webshop" with likelihood 5 and impact 5
    When I add the objective "Few high risks" measured by the tracker as "OpenHighRisks" with the target at most 1
    And the tracker takes its measurements
    Then the objective "Few high risks" shows "2" in red

  @US-209 @iso27001-9.1 @integration
  Scenario: The tracker counts only the projects inside the ISMS scope
    Given I add the risk "Old server" to "Old intranet" with likelihood 5 and impact 5
    When I add the objective "Few high risks" measured by the tracker as "OpenHighRisks" with the target at most 1
    And the tracker takes its measurements
    Then the objective "Few high risks" shows "0" in green

  @US-209 @iso27001-9.1 @integration
  Scenario: The tracker measures again only when the frequency has passed
    Given I add the objective "Few high risks" measured by the tracker as "OpenHighRisks" with the target at most 1
    And the tracker takes its measurements
    When the tracker takes its measurements
    Then the objective "Few high risks" has 1 measurement

  @US-209 @iso27001-9.1 @integration
  Scenario: A manual objective below its target is red
    Given the manual objective "Phishing test click rate" with the target at most 5 "%"
    When I enter the measurement "12" for "Phishing test click rate"
    Then the objective "Phishing test click rate" shows "12 %" in red
    And the activity log has "ObjectiveChanged" saying "Measured Phishing test click rate: 12 %"

  @US-209 @iso27001-9.1 @integration
  Scenario: A manual objective without a measurement is due
    Given the manual objective "Phishing test click rate" with the target at most 5 "%"
    When I open the objectives
    Then the objective "Phishing test click rate" is due for a measurement

  @US-209 @iso27001-9.1 @integration
  Scenario: Correcting the tracker needs a note
    Given I add the objective "Few high risks" measured by the tracker as "OpenHighRisks" with the target at most 1
    And the tracker takes its measurements
    When I enter the measurement "1" for "Few high risks"
    Then the objectives page says "Say why you correct the tracker's measurement"

  @US-209 @iso27001-6.2 @integration
  Scenario: A member who does not decide compliance reads the objectives
    Given the manual objective "Phishing test click rate" with the target at most 5 "%"
    And "Dan" has joined "Acme ApS" as "Developer"
    And I am signed in as "Dan"
    When I open the objectives
    Then the objectives page is read-only
