Feature: Withdraw an upload made by mistake

  # US-177: a scan, an SBOM or test results uploaded by mistake is withdrawn with a reason. What it brought leaves the
  # project; what it changed is as before; the history is kept.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-177 @iso27001-A.5.33 @integration
  Scenario: A scan in the wrong project is withdrawn with its findings
    Given "Acme Portal" has a "trivy" scan with the findings "CVE-1" and "CVE-2"
    When I withdraw the newest "trivy" scan because "Wrong project"
    Then "Acme Portal" has no open findings
    And the Scans tab lists no "trivy" scan
    And the activity log for "Acme ApS" has "ScanWithdrawn" for "Anna"

  @US-177 @integration
  Scenario: The findings a withdrawn scan closed are open again
    Given "Acme Portal" has a "trivy" scan with the findings "CVE-1" and "CVE-2"
    When I upload a "trivy" scan with only the finding "CVE-1"
    And I withdraw the newest "trivy" scan because "Scanned the wrong branch"
    Then the open findings of "Acme Portal" are "CVE-1" and "CVE-2"

  @US-177 @integration
  Scenario: Only the newest scan of a tool can be withdrawn, and only with a reason
    Given "Acme Portal" has a "trivy" scan with the findings "CVE-1" and "CVE-2"
    When I upload a "trivy" scan with only the finding "CVE-1"
    And I try to withdraw the oldest "trivy" scan because "Wrong project"
    Then I see the error "Only the newest scan of a tool can be withdrawn"
    When I withdraw the newest "trivy" scan because ""
    Then I see the error "Give a reason for withdrawing it"

  @US-177 @cra-annex-I @integration
  Scenario: A wrong SBOM is withdrawn and the previous one is current again
    Given I upload the CycloneDX SBOM "first.json" with 3 components for version "2.0"
    And I upload the CycloneDX SBOM "wrong.json" with 9 components for version "2.0"
    When I withdraw the SBOM for version "2.0" because "Another product"
    Then the SBOM for version "2.0" has 3 components

  @US-177 @integration
  Scenario: Test results uploaded by mistake are withdrawn
    Given I have uploaded test results where 2 scenarios tagged "@cra-I.2(d)" passed
    When I withdraw the test results because "Results of another product"
    Then no test results wait to be withdrawn
    And the activity log for "Acme ApS" has "TestResultsWithdrawn" for "Anna"

  @US-177 @iso27001-A.8.15 @integration
  Scenario: A specification version imported by mistake is withdrawn
    Given "Acme Portal" has version 1 of "kravspec.xlsx" with "REQ-003 Log in" and "REQ-005 Print"
    When I upload version 2 with "REQ-003 Log in with MitID" and "REQ-020 Export"
    And I withdraw the newest version of "kravspec.xlsx" because "Another product's file"
    Then the requirement "REQ-005" is still active
    And the requirement "REQ-020" is marked "Removed in version 2"
    And the activity log for "Acme ApS" has "RequirementDocumentVersionWithdrawn" for "Anna"
