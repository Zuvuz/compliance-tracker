Feature: The lifecycle phase and its checklist in the reports

  # US-194: the project's phase, every phase's checklist (done, missing, turned off with the reason) and the phase changes
  # with what was missing at the time are in the traceability report, the technical documentation and the audit package;
  # the audit check lists what is missing in the current phase.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-194 @cra-annex7 @integration
  Scenario: The audit package shows the phase changes with what was missing
    Given "Acme Portal" is in the phase "Design"
    And I change the phase of "Acme Portal" to "Development"
    When I create the audit package of "Acme Portal"
    Then the audit package's file "lifecycle.txt" says "Design → Development"
    And the audit package's file "lifecycle.txt" says "missing: Essential requirements designed in"
    And the audit package's file "lifecycle.txt" says "[Missing] Threat model made – CRA Art. 13(3)"

  @US-194 @iso27001-9.2 @integration
  Scenario: The traceability workbook has the checklist with turned-off items and their reason
    Given "Acme Portal" is in the phase "Verification"
    And I turn off the checklist item "Coordinated vulnerability disclosure policy published" of "Acme Portal" because "Handled by the customer"
    When I download the traceability matrix of "Acme Portal" as Excel for an auditor
    Then on "Lifecycle checklist" the row "Coordinated vulnerability disclosure policy published" says "Turned off" and "Reason: Handled by the customer"
    And on "Lifecycle checklist" the row "SBOM uploaded" says "Missing" and ""

  @US-194 @iso27001-9.3 @integration
  Scenario: The management report shows the phase and how much is done
    Given "Acme Portal" is in the phase "Verification"
    When I download the traceability matrix of "Acme Portal" as Excel for the management
    Then the summary says "Lifecycle phase" is "Verification · 0 of 4 done"

  @US-194 @integration
  Scenario: The audit check lists what is missing in the current phase
    Given "Acme Portal" is in the phase "Verification"
    When I check the audit package of "Acme Portal"
    Then the check says "Checklist of the phase Verification" is missing
