Feature: The CRA's requirements come with the tracker

  # US-043: Annex I of the CRA comes with every database, with the official text from EUR-Lex.
  # A requirement can be marked "Not applicable" for a project, with a reason, and is then left out of the coverage.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-043 @cra-annexI @integration
  Scenario: The CRA's requirements are there
    When I open the "CRA" sub-tab of "Acme Portal"
    Then I see the group "Part I – Essential cybersecurity requirements" with 14 requirements, starting with "I.1"
    And I see the group "Part II – Vulnerability handling" with 8 requirements, starting with "II.1"
    And each requirement links to the official text
    And the coverage says "0 of 22 requirements covered (0 %)"

  @US-043 @integration
  Scenario: The CRA's requirements cannot be changed
    When I try to add the requirement "I.99 Extra" to the built-in "CRA – Regulation (EU) 2024/2847"
    Then I see the error "Built-in frameworks cannot be changed"

  @US-043 @cra-art13 @integration
  Scenario: A requirement that does not apply is left out of the coverage
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    When I mark "I.2(k)" as not applicable for "Acme Portal" with the reason "No network-facing interface"
    Then the row "I.2(k)" is "Not applicable" with the reason "No network-facing interface"
    And the coverage says "1 of 21 requirements covered (5 %), 1 not applicable"

  @US-043 @integration
  Scenario: Not applicable needs a reason
    When I mark "I.2(k)" as not applicable for "Acme Portal" with the reason ""
    Then I see the error "Give a reason why the requirement does not apply"

  @US-043 @integration
  Scenario: A requirement can be made applicable again
    Given "I.2(k)" is not applicable for "Acme Portal"
    When I make "I.2(k)" applicable again for "Acme Portal"
    Then the row "I.2(k)" is "Not covered"

  @US-043 @integration
  Scenario: Not applicable works for uploaded frameworks too
    Given the framework "Security policy" has the active requirements "SP-1 Passwords" and "SP-9 Paper archive"
    And "Acme Portal" uses "Security policy" version 1
    When I mark "SP-9" as not applicable for "Acme Portal" with the reason "No paper records"
    Then the row "SP-9" is "Not applicable" with the reason "No paper records"

  @US-043 @iso27001-A.8.15 @integration
  Scenario: Applicability changes are logged
    When I mark "I.2(k)" as not applicable for "Acme Portal" with the reason "No network-facing interface"
    Then the activity log for "Acme ApS" has "RequirementMarkedNotApplicable" for "Anna" concerning "Acme Portal"
    And the "RequirementMarkedNotApplicable" entry says "I.2(k): No network-facing interface"
