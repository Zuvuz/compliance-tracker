Feature: What a risk has gone from and to

  # US-203: the register shows the score before and after the measures, the matrix can show the risks after the measures
  # (by their residual), and the risk's page shows its history from the activity log.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the risk "Session Hijacking" exists in "Acme Portal"

  @US-203 @iso27001-8.3 @integration
  Scenario: The register shows the score before and after the measures
    Given I set the residual risk of "Session Hijacking" to 2 × 3
    When I open the "Risks" tab of "Acme Portal"
    Then the register shows the score "3 × 4 = 12 → 2 × 3 = 6" for "Session Hijacking"

  @US-203 @integration
  Scenario: Without a residual the register shows the score once
    When I open the "Risks" tab of "Acme Portal"
    Then the register shows the score "3 × 4 = 12" for "Session Hijacking"

  @US-203 @integration
  Scenario: The matrix after the measures
    Given I set the residual risk of "Session Hijacking" to 2 × 3
    When I open the "Risks" tab of "Acme Portal"
    And I show the matrix after the measures
    Then the matrix shows 1 open risk at likelihood 2 and impact 3
    And the matrix shows no open risk at likelihood 3 and impact 4

  @US-203 @integration
  Scenario: A risk without a residual stays in its place after the measures
    When I open the "Risks" tab of "Acme Portal"
    And I show the matrix after the measures
    Then the matrix shows 1 open risk at likelihood 3 and impact 4
    And the matrix says "1 risk has no residual yet"

  @US-203 @iso27001-8.2 @integration
  Scenario: The risk's page shows its history
    Given I set the residual risk of "Session Hijacking" to 2 × 3
    When I open the risk "Session Hijacking"
    Then the history shows "Residual risk: – → 2 × 3 (Medium)" by "Anna"
    And the history shows "likelihood 3, impact 4 (High)" by "Anna"
