Feature: Import an SBOM

  # US-079: the product's SBOM (CycloneDX or SPDX JSON) is uploaded for a product version on the Scans tab. Its
  # components are listed with their package URL and licence, and a component that an open scan finding names is marked.
  # The SBOM can be evidence for a requirement (CRA Annex I, Part II (1)).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-079 @cra-AnnexI @integration
  Scenario: A CycloneDX SBOM is read
    When I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    Then the SBOM for version "2.1.0" says "CycloneDX 1.6 · 3 components · version 2.1.0"
    And the activity log has "SbomImported" saying "CycloneDX 1.6 · 3 components · version 2.1.0 (sbom.cdx.json)"

  @US-079 @integration
  Scenario: An SPDX SBOM is read
    When I upload the SPDX SBOM "sbom.spdx.json" with 2 components for version "2.1.0"
    Then the SBOM for version "2.1.0" lists 2 components
    And the component "Npgsql" has the package URL "pkg:nuget/Npgsql@10.0.3" and the licence "PostgreSQL"

  @US-079 @integration
  Scenario: A new SBOM for the same version replaces the shown one and the old one is kept
    Given I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    When I upload the CycloneDX SBOM "sbom-new.cdx.json" with 4 components for version "2.1.0"
    Then the SBOM for version "2.1.0" lists 4 components
    And "Acme Portal" has 2 SBOMs stored

  @US-079 @cra-AnnexI @integration
  Scenario: A component with an open critical finding is marked
    Given "Acme Portal" has a "Trivy" scan with a critical finding in "openssl" "3.0.1"
    When I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    Then the component "openssl" shows "1 open finding (critical)"
    And the component "lodash" shows no findings

  @US-079 @integration
  Scenario: A file that is not an SBOM is refused
    When I upload the file "notes.json" as the SBOM for version "2.1.0"
    Then I see the error "Only CycloneDX (1.4 or newer) and SPDX 2.3 JSON files can be uploaded"

  @US-079 @integration
  Scenario: An old CycloneDX version is refused
    When I upload a CycloneDX 1.3 SBOM for version "2.1.0"
    Then I see the error "Only CycloneDX 1.4 or newer can be read"

  @US-079 @integration
  Scenario: The product version is needed
    When I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version ""
    Then I see the error "Enter the product version the SBOM is for"

  @US-079 @cra-AnnexI @integration
  Scenario: An SBOM is evidence for a requirement
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    When I use the SBOM for version "2.1.0" as evidence for "II.1"
    And I open the "CRA" sub-tab of "Acme Portal"
    Then the row "II.1" has the evidence "SBOM 2.1.0" with its SHA-256
