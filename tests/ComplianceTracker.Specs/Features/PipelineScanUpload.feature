Feature: The build pipeline uploads scan results
  US-122: a build pipeline sends its SARIF file to POST /api/projects/{id}/scans with an upload token for the project,
  together with the commit and the version (CRA Annex I, Part II (3); ISO/IEC 27001 A.8.8, A.8.29). The token can only
  upload, is shown once, kept as a SHA-256 hash and can be revoked (ASVS V2, V13).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And I have created the upload token "GitHub Actions" for "Acme Portal"

  @US-122 @cra-annexI @iso27001-A.8.29 @integration
  Scenario: A pipeline uploads a scan with its commit and version
    When the pipeline uploads a "Trivy" scan with 2 findings for commit "3f2a9c1" and version "2.3.1"
    Then the answer is 201 with "Trivy: 2 new, 0 fixed, 2 open"
    And the scan shows "Commit 3f2a9c1 · version 2.3.1"
    And the upload token "GitHub Actions" was last used today

  @US-122 @asvs-V2 @integration
  Scenario: The token is shown once and only its hash is kept
    Then the token was shown once
    And the database keeps only the hash of the token

  @US-122 @asvs-V2 @integration
  Scenario: A revoked token is refused
    Given I have revoked the upload token "GitHub Actions"
    When the pipeline uploads a "Trivy" scan with 2 findings
    Then the answer is 401

  @US-122 @asvs-V13 @integration
  Scenario: A token cannot upload to another project
    Given the project "Beta App" exists
    When the pipeline uploads a "Trivy" scan to "Beta App" with the token of "Acme Portal"
    Then the answer is 401

  @US-122 @asvs-V13 @integration
  Scenario: A wrong or missing token is refused
    When the pipeline uploads a "Trivy" scan with the token "ctu_not-a-real-token"
    Then the answer is 401
    When the pipeline uploads a "Trivy" scan without a token
    Then the answer is 401

  @US-122 @integration
  Scenario: Only SARIF files are accepted
    When the pipeline uploads the file "report.html" with "<html>Not a scan</html>"
    Then the answer is 415

  @US-122 @integration
  Scenario: A wrong commit is refused
    When the pipeline uploads a "Trivy" scan with 2 findings for commit "not-a-sha" and version "2.3.1"
    Then the answer is 400 with "The commit must be a Git SHA of 7 to 40 hex characters"

  @US-122 @integration
  Scenario: At most 30 uploads an hour with one token
    Given the pipeline has uploaded 30 scans within the hour
    When the pipeline uploads a "Trivy" scan with 2 findings
    Then the answer is 429
