Feature: Signing a locked report in the tracker, sealed by the company

  # US-132: a locked report (US-050) is signed by a compliance officer and a manager, two different people, each after a
  # fresh sign-in. When both have signed, the PDF gets a page with the signers and is sealed with the organisation's
  # certificate as a digital signature.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Bo" has joined "Acme ApS" as "Developer"
    And "Bo" signs for the management of "Acme ApS"
    And I have locked the traceability report of "Acme Portal" for "management"

  @US-132 @iso27001-5.1 @iso27001-A.5.33 @integration
  Scenario: Both roles sign and the report is sealed
    Given the organisation has uploaded the seal "Acme ApS"
    And signatures are asked for on "Traceability – Acme Portal (Management)"
    When "Anna" signs "Traceability – Acme Portal (Management)" as "ComplianceOfficer"
    And "Bo" signs "Traceability – Acme Portal (Management)" as "Manager"
    Then "Traceability – Acme Portal (Management)" is sealed by "Acme ApS"
    And the sealed PDF has a valid signature over the whole file
    And the sealed PDF lists "Anna, Compliance officer (Organisation admin)" and "Bo, Manager (Developer)"
    And the activity log has "ReportSealed" saying "sealed by CN=Acme ApS"

  @US-132 @iso27001-A.5.3 @integration
  Scenario: One person cannot sign in two roles
    Given "Anna" signs for the management of "Acme ApS"
    And signatures are asked for on "Traceability – Acme Portal (Management)"
    And "Anna" signs "Traceability – Acme Portal (Management)" as "ComplianceOfficer"
    When "Anna" tries to sign "Traceability – Acme Portal (Management)" as "Manager"
    Then the report has 1 signature

  @US-132 @asvs-V2.3 @integration
  Scenario: Signing needs a fresh sign-in
    Given signatures are asked for on "Traceability – Acme Portal (Management)"
    When "Anna" signed in 30 minutes ago and signs "Traceability – Acme Portal (Management)" as "ComplianceOfficer"
    Then "Anna" is asked to sign in again
    And the report has 0 signatures

  @US-132 @integration
  Scenario: Without a seal the report is signed but not sealed, and sealed later
    Given signatures are asked for on "Traceability – Acme Portal (Management)"
    And "Anna" signs "Traceability – Acme Portal (Management)" as "ComplianceOfficer"
    And "Bo" signs "Traceability – Acme Portal (Management)" as "Manager"
    Then the Reports tab says "Signed; not sealed yet"
    When the organisation has uploaded the seal "Acme ApS"
    And I seal "Traceability – Acme Portal (Management)"
    Then "Traceability – Acme Portal (Management)" is sealed by "Acme ApS"

  @US-132 @integration
  Scenario: An expired certificate cannot be the seal
    When the organisation uploads a seal that expired yesterday
    Then I see the error that starts with "The certificate expired on"

  @US-132 @integration
  Scenario: A wrong password is refused
    When the organisation uploads the seal "Acme ApS" with the wrong password
    Then I see the error "The file is not a certificate (.pfx), or the password is wrong"

  @US-132 @integration
  Scenario: The report waits for the signer's signature
    Given signatures are asked for on "Traceability – Acme Portal (Management)"
    Then "Needs attention" of "Acme Portal" shows 'Waiting for your signature: Traceability – Acme Portal (Management)' for "Bo"

  @US-132 @iso27001-A.5.15 @integration
  Scenario: A member who does not sign for the management cannot sign as Manager
    Given "Carl" has joined "Acme ApS" as "ComplianceManager"
    And signatures are asked for on "Traceability – Acme Portal (Management)"
    When "Carl" tries to sign "Traceability – Acme Portal (Management)" as "Manager"
    Then the report has 0 signatures
