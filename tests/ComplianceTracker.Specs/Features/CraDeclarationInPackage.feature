Feature: CRA technical documentation and EU declaration of conformity in the audit package

  # US-181: a CRA project's audit package has the draft technical documentation (Annex VII) and a draft EU declaration
  # of conformity (Annex V), filled in from the tracker. What the tracker does not know says "To be completed", and the
  # package's check lists it. The drafts are never presented as signed.

  Background:
    Given an empty database
    And the time is "2026-11-16T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on

  @US-181 @cra-AnnexV @integration
  Scenario: A CRA project's package has the declaration
    Given "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" is classified under "CRA – Regulation (EU) 2024/2847" as "Important, class I"
    And I upload the CycloneDX SBOM "sbom.cdx.json" with 3 components for version "2.1.0"
    When I create the audit package of "Acme Portal"
    Then the audit package contains "CRA-technical-documentation.pdf"
    And the audit package's file "CRA-declaration-of-conformity.pdf" says "Module B+C or H"
    And the audit package's file "CRA-declaration-of-conformity.pdf" says "Version: 2.1.0"
    And the audit package's file "CRA-declaration-of-conformity.pdf" says "Signature: not signed (draft)"
    And the audit package's file "00-check.pdf" says "Manufacturer's address: To be completed"

  @US-181 @cra-AnnexV @integration
  Scenario: Without a classification the declaration says what to do
    Given "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    When I create the audit package of "Acme Portal"
    Then the audit package's file "CRA-declaration-of-conformity.pdf" says "To be completed: classify the product"
    And the audit package's file "CRA-declaration-of-conformity.pdf" says "Version: To be completed"
    And the audit package's file "00-check.pdf" says "Conformity assessment procedure: classify the product"

  @US-181 @integration
  Scenario: A project that does not follow the CRA has neither draft
    Given "AI Act – Regulation (EU) 2024/1689" is switched on
    And "Acme Portal" uses "AI Act – Regulation (EU) 2024/1689"
    When I create the audit package of "Acme Portal"
    Then the audit package has 0 files under "CRA-declaration"
    And the audit package has 0 files under "CRA-technical"
