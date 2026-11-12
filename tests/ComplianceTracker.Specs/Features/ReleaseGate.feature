Feature: Gate from Development to Released

  # US-072: before the product is released, the Overview shows what must be in place: every applicable requirement
  # covered, evidence for each covered one, no open critical or high findings, an SBOM and a support period. Releasing
  # with a check not met needs a reason, as in US-071.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" is in phase "Development"

  @US-072 @cra-AnnexI @integration
  Scenario: Requirements without evidence are a missing check
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    When I open the overview of "Acme Portal"
    Then the gate "Ready for release" shows "1 requirement without evidence" as "Not met"

  @US-072 @iso27001-A.8.29 @integration
  Scenario: Open critical findings are a missing check
    Given "Acme Portal" has a "Trivy" scan with a critical finding in "openssl" "3.0.1"
    When I open the overview of "Acme Portal"
    Then the gate "Ready for release" shows "1 open critical finding" as "Not met"

  @US-072 @iso27001-A.8.29 @integration
  Scenario: Without a scan the product has not been tested
    When I open the overview of "Acme Portal"
    Then the gate "Ready for release" shows "No security scan yet" as "Not met"

  @US-072 @cra-AnnexI @integration
  Scenario: The SBOM check is met by an uploaded SBOM
    Given I open the overview of "Acme Portal"
    And the gate "Ready for release" shows "No SBOM uploaded" as "Not met"
    When I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    And I open the overview of "Acme Portal"
    Then the gate "Ready for release" shows "SBOM uploaded (version 2.1.0)" as "Met"

  @US-072 @cra-Art.13 @integration
  Scenario: Releasing with missing checks needs a reason
    When I release "Acme Portal" with support until "2032-12-01" and no gate reason
    Then I see the error "Give a reason, as 3 checks are not met"
    And the page of "Acme Portal" shows phase "Development"

  @US-072 @iso27001-A.8.15 @integration
  Scenario: The reason for releasing is logged
    When I release "Acme Portal" with support until "2032-12-01" and the gate reason "Pilot customer only"
    Then the page of "Acme Portal" shows phase "Released"
    And the "ProjectPhaseChanged" entry says "Phase: Development → Released, 3 checks not met: Pilot customer only"

  @US-072 @integration
  Scenario: A released project shows no gate
    Given "Acme Portal" is in phase "Released"
    Then the Overview of "Acme Portal" shows no gate
