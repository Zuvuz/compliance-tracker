Feature: Evidence grouped by type

  # US-201: the Evidence tab groups the evidence by type in foldable groups, in a fixed order, with a count each. A group
  # starts folded unless something in it waits for a decision or has expired; what the member opens or folds is remembered
  # in the browser (site.js). Evidence made from test results, an SBOM, a scan or a locked report has a group of its own.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-201 @iso27001-7.5.3 @integration
  Scenario: The evidence is grouped by type with a count
    Given I have added the evidence "Pentest report" with the file "pentest.pdf" to "I.2(d)"
    And I have added the evidence "Branch protection" with the link "https://github.com/acme/portal/settings/branches" to "I.2(d)"
    And I have added the evidence "MFA config" with the link "https://github.com/acme/portal/blob/main/mfa.md" to "I.2(b)"
    When I open the Evidence tab of "Acme Portal"
    Then the evidence groups are "Files (1), Links (2)"
    And every evidence group is folded

  @US-201 @integration
  Scenario: A group with something waiting is open
    Given "Access policy 2026" has the suggestion "I.2(d)"
    When I open the Evidence tab of "Acme Portal"
    Then the evidence group "Files" is open and says "1 waiting"

  @US-201 @integration
  Scenario: Accepted test results have their own group
    Given I have uploaded test results where 3 scenarios tagged "@cra-I.2(d)" passed
    When I accept the suggested evidence "Tests passed: 3 scenarios"
    And I open the Evidence tab of "Acme Portal"
    Then the evidence "Tests passed: 3 scenarios" is in the group "Test results"

  @US-201 @integration
  Scenario: An SBOM used as evidence has its own group
    Given I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    When I use the SBOM for version "2.1.0" as evidence for "II.1"
    And I open the Evidence tab of "Acme Portal"
    Then the evidence "SBOM 2.1.0" is in the group "SBOMs"
