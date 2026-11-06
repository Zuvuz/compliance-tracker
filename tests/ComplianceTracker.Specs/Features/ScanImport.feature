Feature: Import scan results
  US-078: a project's security scanners deliver SARIF 2.1.0 files. Each result becomes a finding with its severity; a
  new scan from the same tool marks findings it no longer reports as fixed. A finding can be accepted with a reason,
  and a scan can be evidence for a requirement (CRA Annex I, ISO 27001 A.8.8).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-078 @iso27001-A.8.8 @integration
  Scenario: Findings are read from a SARIF file
    When I upload the SARIF file "trivy.sarif" from "Trivy" with 3 findings, 1 of them critical
    Then I see the scan result "Trivy: 3 new, 0 fixed, 3 open (1 critical)"
    And the finding "CVE-2026-1001" is open with the severity "Critical"
    And the finding "CVE-2026-1002" is open with the severity "High"

  @US-078 @cra-annexI @integration
  Scenario: A finding that is no longer reported is fixed
    Given "Acme Portal" has a "Trivy" scan with the findings "CVE-2026-1111" and "CVE-2026-2222"
    When I upload a "Trivy" scan with only the finding "CVE-2026-2222"
    Then I see the scan result "Trivy: 0 new, 1 fixed, 1 open"
    And the finding "CVE-2026-1111" is marked "Fixed in scan of" today

  @US-078 @iso27001-A.8.8 @integration
  Scenario: A finding can be accepted with a reason
    Given "Acme Portal" has a "Trivy" scan with the findings "CVE-2026-1111" and "CVE-2026-2222"
    When I accept the finding "CVE-2026-2222" because "Not reachable in our use"
    Then the finding "CVE-2026-2222" is marked "Accepted: Not reachable in our use"
    And the finding "CVE-2026-1111" is open with the severity "High"

  @US-078 @asvs-V5.2.2 @integration
  Scenario: Other files are refused
    When I upload the scan "report.html"
    Then I see the error "Only SARIF files can be uploaded"

  @US-078 @cra-annexI @integration
  Scenario: A scan is evidence for a requirement
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" has a "Trivy" scan with the findings "CVE-2026-1111" and "CVE-2026-2222"
    When I use the "Trivy" scan as evidence for "II.3"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "II.3" has the evidence "Trivy scan" with its SHA-256

  @US-078 @integration
  Scenario: Open high and critical findings need attention
    Given "Acme Portal" has a "Trivy" scan with the findings "CVE-2026-1111" and "CVE-2026-2222"
    When "Anna" opens the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "2 open high or critical findings"
