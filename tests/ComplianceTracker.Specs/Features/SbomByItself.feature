Feature: The SBOM comes by itself

  # US-148: a pipeline sends the SBOM with the project's upload token (US-122), or the tracker fetches GitHub's SBOM from
  # the dependency graph, so every release has an up-to-date SBOM without anyone uploading it by hand.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And I have created the upload token "GitHub Actions" for "Acme Portal"

  @US-148 @cra-AnnexI @integration
  Scenario: A pipeline sends the SBOM with its token
    When the pipeline sends a CycloneDX SBOM with 3 components for version "2.1.0" with the token
    Then the answer is 201
    And the SBOM for version "2.1.0" says "CycloneDX 1.6 · 3 components · version 2.1.0"
    And the SBOM for version "2.1.0" comes from "Pipeline (GitHub Actions)"
    And the activity log has "SbomImported" saying "Pipeline (GitHub Actions)"

  @US-148 @integration
  Scenario: A revoked token is refused
    Given I have revoked the upload token "GitHub Actions"
    When the pipeline sends a CycloneDX SBOM with 3 components for version "2.1.0" with the token
    Then the answer is 401
    And "Acme Portal" has 0 SBOMs stored

  @US-148 @integration
  Scenario: An SBOM without a version is refused
    When the pipeline sends a CycloneDX SBOM with 3 components without a version
    Then the answer is 400
    And the answer says "Send the product version"
    And "Acme Portal" has 0 SBOMs stored

  @US-148 @cra-AnnexI @integration
  Scenario: The SBOM is fetched from GitHub
    Given "Acme Portal" is linked to "acme/portal"
    And GitHub has an SBOM with 2 packages for "acme/portal"
    When I fetch the SBOM from GitHub for "Acme Portal"
    Then the SBOM for version "GitHub (default branch)" says "SPDX 2.3 · 2 components · version GitHub (default branch)"
    And the SBOM for version "GitHub (default branch)" comes from "GitHub"

  @US-148 @integration
  Scenario: A repository without a dependency graph says so
    Given "Acme Portal" is linked to "acme/portal"
    When I fetch the SBOM from GitHub for "Acme Portal"
    Then the Scans tab says "GitHub has no dependency graph for acme/portal"
    And "Acme Portal" has 0 SBOMs stored
