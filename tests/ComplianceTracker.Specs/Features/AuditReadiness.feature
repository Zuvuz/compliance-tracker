Feature: Check the audit package before it is made

  # US-178: before an audit package is made, the tracker checks per framework what an auditor usually asks for (ISO/IEC
  # 27001's key clauses; the CRA's SBOM, support period, vulnerability contact and risk assessment; valid evidence for
  # every applicable requirement; evidence that ends soon) and links each missing item to where it is fixed. The package
  # can still be made, and the check is in it as 00-check.pdf (US-197).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-178 @cra-AnnexVII @integration
  Scenario: The check lists what is missing, with where to fix it
    When I check the audit package of "Acme Portal"
    Then the check is not ready
    And the check says "Software bill of materials (Annex I, Part II (1))" is missing
    And the missing "Software bill of materials (Annex I, Part II (1))" links to the "scans" tab

  @US-178 @integration
  Scenario: What is there is shown as there
    Given the risk "Stolen sessions" exists in "Acme Portal"
    When I check the audit package of "Acme Portal"
    Then the check says "Cybersecurity risk assessment (Art. 13(2))" is there

  @US-178 @integration
  Scenario: The package holds the check
    When I create the audit package of "Acme Portal"
    Then the audit package contains "00-check.pdf"
    And the package's check says "[ ] Software bill of materials (Annex I, Part II (1)): No SBOM uploaded"
