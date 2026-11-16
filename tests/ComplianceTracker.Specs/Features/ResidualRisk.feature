Feature: Residual risk after the measures

  # US-165: a risk shows its level before and after the measures. The level before never changes by itself; a person
  # sets the residual risk, and is asked to when every measure is backed by a story or evidence.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the risk "Account takeover" exists in "Acme Portal"

  @US-165 @iso27001-6.1.3 @integration
  Scenario: A backed risk asks for its residual risk
    Given the risk "Account takeover" has the requirement "I.2(d)" as a measure
    And I have added the evidence "MFA policy" with the link "https://example.com/mfa" to "I.2(d)"
    When I open the "Risks" tab of "Acme Portal"
    Then the risk "Account takeover" is marked "Residual to set"
    And the risk "Account takeover" shows "High → –"

  @US-165 @iso27001-8.3 @integration
  Scenario: The register shows the risk before and after
    When I set the residual risk of "Account takeover" to 2 × 3
    And I open the "Risks" tab of "Acme Portal"
    Then the risk "Account takeover" shows "High → Medium"
    And the activity log has "RiskResidualSet" saying "Account takeover: Residual risk: – → 2 × 3 (Medium)"

  @US-165 @integration
  Scenario: The residual cannot be higher than the risk before measures
    When I set the residual risk of "Account takeover" to 5 × 5
    Then the risk page says "The residual risk cannot be higher than the risk before measures"

  @US-165 @integration
  Scenario: A measure removed after the residual was set asks to review it
    Given the risk "Account takeover" has the requirement "I.2(d)" as a measure
    And I set the residual risk of "Account takeover" to 2 × 3
    When I remove the measure "I.2(d)" from "Account takeover"
    And I open the "Risks" tab of "Acme Portal"
    Then the risk "Account takeover" is marked "Residual to review"
