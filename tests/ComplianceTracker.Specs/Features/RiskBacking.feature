Feature: The risk register shows what is behind each risk

  # US-159: each risk in the register shows its measures, the stories behind them and the evidence, and an open High or
  # Critical risk to reduce with nothing behind it stands out, with its own filter and in "Needs attention".

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-159 @iso27001-6.1.3 @integration
  Scenario: Each risk shows what it is linked to
    Given the risk "Account takeover" exists in "Acme Portal"
    And the risk "Account takeover" has the requirement "I.2(d)" as a measure
    And the project "Acme Portal" has the user story "[US-004] Sign in with MFA"
    And the user story "[US-004] Sign in with MFA" is linked to the requirement "I.2(d)"
    And I have added the evidence "MFA policy" with the link "https://example.com/mfa" to "I.2(d)"
    When I open the "Risks" tab of "Acme Portal"
    Then the risk "Account takeover" is linked to "1 measure · 1 story · 1 evidence"
    And the risk "Account takeover" is not marked "Nothing behind it"

  @US-159 @integration
  Scenario: A serious risk with nothing behind it stands out
    Given the risk "Data leak" exists in "Acme Portal"
    And the risk "Data leak" has the requirement "I.2(e)" as a measure
    When I open the "Risks" tab of "Acme Portal"
    Then the risk "Data leak" is linked to "1 measure · 0 stories · 0 evidence"
    And the risk "Data leak" is marked "Nothing behind it"

  @US-159 @integration
  Scenario: Only the risks with nothing behind them can be shown
    Given the risk "Data leak" exists in "Acme Portal"
    And the risk "Account takeover" exists in "Acme Portal"
    And the risk "Account takeover" has the requirement "I.2(d)" as a measure
    And I have added the evidence "MFA policy" with the link "https://example.com/mfa" to "I.2(d)"
    When I show only the risks of "Acme Portal" with nothing behind them
    Then the register lists only "Data leak"

  @US-159 @integration
  Scenario: An accepted risk is not marked
    Given the risk "Vendor outage" exists in "Acme Portal"
    And I accept the risk "Vendor outage" with the reason "The vendor's SLA covers it"
    When I open the "Risks" tab of "Acme Portal"
    Then the risk "Vendor outage" is not marked "Nothing behind it"

  @US-159 @integration
  Scenario: Needs attention counts them
    Given the risk "Data leak" exists in "Acme Portal"
    Then the overview says "1 High or Critical risk with nothing behind it"
