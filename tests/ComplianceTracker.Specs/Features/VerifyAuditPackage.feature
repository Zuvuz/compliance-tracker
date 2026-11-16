Feature: A signed audit package that the receiver can verify

  # US-180: every audit package has manifest.json with each file's SHA-256; with the organisation's seal it is signed
  # (manifest.json.p7s). Anyone can check a package on the public page "Verify an audit package"; nothing is stored.

  Background:
    Given an empty database
    And the time is "2026-11-16T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And I have added the evidence "MFA config" with the file "mfa.pdf" to "I.2(d)"

  @US-180 @iso27001-A.8.24 @integration
  Scenario: An unchanged package is verified
    Given the organisation has uploaded the seal "Acme ApS"
    And I have created the audit package of "Acme Portal"
    When someone verifies the package without signing in
    Then the verify page says "unchanged"
    And the verify page names "Acme ApS" and "Anna"

  @US-180 @iso27001-A.5.33 @integration
  Scenario: A changed file is found
    Given the organisation has uploaded the seal "Acme ApS"
    And I have created the audit package of "Acme Portal"
    When someone verifies the package with "evidence/MFA-config-mfa.pdf" changed
    Then the verify page says "changed"
    And the verify page lists "Changed" "evidence/MFA-config-mfa.pdf"

  @US-180 @integration
  Scenario: A removed and an added file are found
    Given the organisation has uploaded the seal "Acme ApS"
    And I have created the audit package of "Acme Portal"
    When someone verifies the package with "evidence/MFA-config-mfa.pdf" removed and "extra.txt" added
    Then the verify page lists "Missing" "evidence/MFA-config-mfa.pdf"
    And the verify page lists "Added" "extra.txt"

  @US-180 @integration
  Scenario: Without a seal the package is unsigned, and its files can still be checked
    Given I have created the audit package of "Acme Portal"
    When someone verifies the package without signing in
    Then the verify page says "unsigned"
    And the audit package's contents say "Unsigned package: the files can be checked against manifest.json, the maker cannot."

  @US-180 @integration
  Scenario: A file that is not an audit package is refused
    When someone verifies a file that is not a ZIP
    Then the verify page says "problem"
