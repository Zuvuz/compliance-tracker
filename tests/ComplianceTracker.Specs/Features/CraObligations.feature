Feature: The CRA's obligations beyond Annex I

  # US-069: version 2 of the built-in CRA adds the manufacturer's obligations in Art. 13, 14 and 28–32 and Annexes II
  # and VII, with the official text from EUR-Lex. Annex I keeps its codes, so a project that moves from version 1 keeps
  # its links, "not applicable" decisions and evidence.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on

  @US-069 @cra-Art.14 @integration
  Scenario: The reporting obligations are requirements, with their deadlines
    Given "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then I see the group "Art. 14 – Reporting obligations" with 10 requirements, starting with "Art.14(1)"
    And the requirement "Art.14(2)(a)" says "Early warning of an actively exploited vulnerability within 24 hours"
    And the requirement "Art.14(4)(c)" says "Final report on the incident within one month"

  @US-069 @cra-Art.13 @integration
  Scenario: The manufacturer's obligations and the conformity assessment are requirements
    Given "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then I see the group "Art. 13 – Obligations of manufacturers" with 19 requirements, starting with "Art.13(2)"
    And I see the group "Art. 28–32 – Conformity assessment and CE marking" with 4 requirements, starting with "Art.28"

  @US-069 @cra-AnnexII @cra-AnnexVII @integration
  Scenario: Annex II and Annex VII are requirements
    Given "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I open the "CRA" sub-tab of "Acme Portal"
    Then I see the group "Annex II – Information and instructions to the user" with 9 requirements, starting with "Annex II.1"
    And I see the group "Annex VII – Technical documentation" with 8 requirements, starting with "Annex VII.1"
    And the requirement "Annex II.7" says "The type of technical security support and the end date of the support period"
    And each requirement links to the official text

  @US-069 @cra-AnnexI @integration
  Scenario: Version 2 keeps Annex I, and version 1 is not changed
    Then version 2 of "CRA – Regulation (EU) 2024/2847" has every code of version 1 with the same text
    And version 1 of "CRA – Regulation (EU) 2024/2847" still has 22 requirements

  @US-069 @integration
  Scenario: A project on version 1 is told about version 2
    Given "Acme Portal" uses "CRA – Regulation (EU) 2024/2847" version 1
    Then the page of "Acme Portal" says "CRA – Regulation (EU) 2024/2847" has a newer version

  @US-069 @iso27001-A.8.32 @integration
  Scenario: Links, "not applicable" and evidence follow the project to version 2
    Given "Acme Portal" uses "CRA – Regulation (EU) 2024/2847" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And I mark "I.2(k)" as not applicable for "Acme Portal" with the reason "No network-facing interface"
    And I have added the evidence "MFA settings" with the link "https://example.com/mfa" to "I.2(d)"
    And I have added the evidence "Pen test" with the file "pentest.pdf" to "II.3"
    When I move "Acme Portal" to the newest version of "CRA – Regulation (EU) 2024/2847"
    Then the project "Acme Portal" uses "CRA – Regulation (EU) 2024/2847" version 2
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" is "Covered" by "[US-004] Sign in"
    And the row "I.2(k)" is "Not applicable" with the reason "No network-facing interface"
    And the row "I.2(d)" shows the evidence "MFA settings"
    And the row "II.3" shows the evidence "Pen test" with its SHA-256
    And the activity log of "Acme Portal" says "carried over 1 links, 1 not applicable, 2 evidence"
