Feature: The ISMS scope and context of the organisation

  # US-208: the organisation describes the scope of its ISMS once: which projects it covers and which not (with the reason),
  # the internal and external issues, and the interested parties with their requirements (ISO/IEC 27001 4.1–4.4). The scope
  # is approved as a version, has an owner and a review date, and can be locked as a PDF. "ISMS" is its own area in the
  # top bar.

  Background:
    Given an empty database
    And the time is "2026-11-21T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Webshop" exists
    And the project "Old intranet" exists

  @US-208 @iso27001-4.3 @integration
  Scenario: The scope names the projects inside and outside
    When I put "Webshop" inside the ISMS scope
    And I put "Old intranet" outside the ISMS scope because "is shut down in March 2027"
    Then the ISMS scope shows "Webshop" as "Inside"
    And the ISMS scope shows "Old intranet" as "Outside" with the reason "is shut down in March 2027"
    And the activity log has "IsmsScopeChanged" saying "Old intranet: outside the scope, because is shut down in March 2027"

  @US-208 @iso27001-4.3 @integration
  Scenario: A new project is outside until someone decides
    When I open the ISMS scope
    Then the ISMS scope shows "Webshop" as "NotDecided"

  @US-208 @iso27001-4.3 @integration
  Scenario: A project outside the scope needs a reason
    When I put "Old intranet" outside the ISMS scope because ""
    Then the ISMS page says "Give the reason the project is outside the scope"

  @US-208 @iso27001-4.2 @integration
  Scenario: An interested party with its requirements
    When I add the interested party "Customers" of the kind "Customer" with the requirement "ISO 27001 certificate" handled and "Data in the EU" not handled
    Then the interested party "Customers" has the requirement "ISO 27001 certificate" handled
    And the interested party "Customers" has the requirement "Data in the EU" not handled
    And the activity log has "InterestedPartyChanged" saying "Added Customers (Customer): 1 of 2 requirements handled"

  @US-208 @iso27001-4.2 @integration
  Scenario: The same interested party cannot be added twice
    Given I have added the interested party "Customers" of the kind "Customer" with the requirement "ISO 27001 certificate" handled and "Data in the EU" not handled
    When I add the interested party "customers" of the kind "Customer" with the requirement "Invoices on time" handled and "Support" not handled
    Then the ISMS page says "customers is already an interested party"

  @US-208 @iso27001-4.4 @integration
  Scenario: Approving the scope makes a version, and a change shows it is no longer the approved one
    Given I have described the ISMS scope as "Development and operation of our SaaS products"
    And I put "Webshop" inside the ISMS scope
    When I approve the ISMS scope
    Then the ISMS scope is "Approved"
    And the activity log has "IsmsScopeApproved" saying "Version 1"
    When I put "Old intranet" outside the ISMS scope because "is shut down in March 2027"
    Then the ISMS scope is "Changed since approval"

  @US-208 @iso27001-4.4 @integration
  Scenario: A scope without a project inside cannot be approved
    Given I have described the ISMS scope as "Development and operation of our SaaS products"
    When I approve the ISMS scope
    Then the ISMS page says "Put at least one project inside the scope before it is approved"

  @US-208 @iso27001-4.4 @integration
  Scenario: The review is overdue
    Given I have described the ISMS scope as "Development and operation of our SaaS products" with the next review on "2026-10-01"
    When I open the ISMS scope
    Then the ISMS scope says the review is overdue

  @US-208 @iso27001-7.5.3 @integration
  Scenario: The approved scope is locked as a PDF
    Given I have described the ISMS scope as "Development and operation of our SaaS products"
    And I put "Webshop" inside the ISMS scope
    And I approve the ISMS scope
    When I lock the ISMS scope
    Then the activity log has "ReportLocked" saying "ISMS scope and context – Acme ApS"

  @US-208 @iso27001-7.5.3 @integration
  Scenario: The scope can be downloaded as a PDF by anyone
    Given "Bo" has joined "Acme ApS" as "Developer"
    And I am signed in as "Bo"
    When I download the ISMS scope as PDF
    Then the ISMS download is a PDF

  @US-208 @iso27001-A.5.15 @integration
  Scenario: A developer reads the scope but cannot change it
    Given "Bo" has joined "Acme ApS" as "Developer"
    And I am signed in as "Bo"
    When I open the ISMS scope
    Then the ISMS scope is read-only
    When I put "Webshop" inside the ISMS scope
    Then the ISMS page says "Your role does not allow this"
    And the ISMS scope shows "Webshop" as "NotDecided"

  @US-208 @integration
  Scenario: ISMS is its own area in the top bar
    When I open "ISMS" from the top bar
    Then I am on the page "/Isms"
    And the top bar has at most 5 items
