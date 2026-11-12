Feature: Gate from Planning to Development

  # US-071: before a project moves from Planning to Development, the Overview shows what must be in place: an intended
  # purpose and scope, a framework, a class under each regulation it follows, and a first risk. The gate warns; moving
  # on with a check not met needs a reason, which is logged.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-071 @cra-Art.13 @integration
  Scenario: The gate shows what is missing
    When I open the overview of "Acme Portal"
    Then the gate "Ready for Development" shows "No risks in the register" as "Not met"
    And the gate "Ready for Development" shows "Follows no framework" as "Not met"
    And the gate "Ready for Development" shows "No intended purpose and scope described" as "Not met"

  @US-071 @iso27001-A.5.8 @integration
  Scenario: Moving on with a missing check needs a reason
    When I move "Acme Portal" to "Development" without a reason
    Then I see the error "Give a reason, as 3 checks are not met"
    And the page of "Acme Portal" shows phase "Planning"

  @US-071 @iso27001-A.8.15 @integration
  Scenario: The reason is logged
    When I move "Acme Portal" to "Development" with the reason "Risk workshop next week"
    Then the page of "Acme Portal" shows phase "Development"
    And the activity log for "Acme ApS" has "ProjectPhaseChanged" for "Anna" concerning "Acme Portal"
    And the "ProjectPhaseChanged" entry says "Phase: Planning → Development, 3 checks not met: Risk workshop next week"

  @US-071 @integration
  Scenario: A check becomes met by itself when the first risk is added
    Given the risk "Phishing" exists in "Acme Portal"
    When I open the overview of "Acme Portal"
    Then the gate "Ready for Development" shows "1 risk in the register" as "Met"

  @US-071 @cra-Art.13 @integration
  Scenario: A regulation the project follows must be classified
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the overview of "Acme Portal"
    Then the gate "Ready for Development" shows "Not classified under CRA" as "Not met"
    When I classify "Acme Portal" under "CRA – Regulation (EU) 2024/2847" as "Default (not important or critical)" because "Not in Annex III or IV"
    And I open the overview of "Acme Portal"
    Then the gate "Ready for Development" shows "Classified under each regulation it follows" as "Met"

  @US-071 @integration
  Scenario: With every check met no reason is needed
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And I classify "Acme Portal" under "CRA – Regulation (EU) 2024/2847" as "Default (not important or critical)" because "Not in Annex III or IV"
    And the risk "Phishing" exists in "Acme Portal"
    And I change the scope of "Acme Portal" to "The customer portal and its API"
    When I move "Acme Portal" to "Development" without a reason
    Then the page of "Acme Portal" shows phase "Development"
    And the "ProjectPhaseChanged" entry says "Phase: Planning → Development, all checks met"

  @US-071 @integration
  Scenario: Moving back to Planning has no gate
    Given "Acme Portal" is in phase "Development"
    When I move "Acme Portal" to "Planning" without a reason
    Then the page of "Acme Portal" shows phase "Planning"
