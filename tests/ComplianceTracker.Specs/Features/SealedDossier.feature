Feature: The audit package as one sealed PDF dossier per framework

  # US-197: each framework is one PDF dossier in the audit package, and the package's own texts are PDFs. When the
  # organisation has a seal (US-132), every PDF the package makes is sealed; the uploaded evidence files stay as they were.

  Background:
    Given an empty database
    And the time is "2026-11-16T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And I have added the evidence "MFA config" with the file "mfa.pdf" to "I.2(d)"

  @US-197 @iso27001-7.5.3 @integration
  Scenario: The package has one sealed dossier per framework
    Given the organisation has uploaded the seal "Acme ApS"
    When I create the audit package of "Acme Portal"
    Then the audit package contains "CRA-dossier.pdf"
    And the package's file "CRA-dossier.pdf" has a valid seal by "Acme ApS"
    And the package's file "00-contents.pdf" has a valid seal by "Acme ApS"
    And the package's file "02-compliance-status.pdf" has a valid seal by "Acme ApS"
    And the audit package's contents say "sealed by"
    And the audit package's contents give "evidence/MFA-config-mfa.pdf" the SHA-256 of the evidence "MFA config"

  @US-197 @iso27001-A.5.33 @integration
  Scenario: A changed dossier shows an invalid seal
    Given the organisation has uploaded the seal "Acme ApS"
    When I create the audit package of "Acme Portal"
    Then the package's file "CRA-dossier.pdf" with one byte changed has no valid seal

  @US-197 @integration
  Scenario: Without a seal the PDFs are made unsealed, and the contents say so
    When I create the audit package of "Acme Portal"
    Then the package's file "CRA-dossier.pdf" is not sealed
    And the audit package's contents say "Not sealed: the organisation has no usable seal"
    And the audit package's file "00-check.pdf" says "Software bill of materials"
