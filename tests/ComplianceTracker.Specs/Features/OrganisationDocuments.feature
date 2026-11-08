Feature: The organisation's documents as shared evidence

  # US-136: policies, procedures and risk tables are kept once for the organisation and linked to the requirements they
  # meet. Every project that follows such a requirement has the document as evidence.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme App" exists
    And "Acme App" uses "CRA – Regulation (EU) 2024/2847"

  @US-136 @iso27001-5.2 @iso27001-A.5.1 @integration
  Scenario: A policy is evidence for every project
    When I upload the policy "Access control policy" version "v1" for the organisation
    And I link the document "Access control policy" to "I.2(d)"
    Then "Acme Portal" has the evidence "Access control policy" for "I.2(d)" from the organisation
    And "Acme App" has the evidence "Access control policy" for "I.2(d)" from the organisation
    And the document "Access control policy" is used by "Acme App, Acme Portal"

  @US-136 @iso27001-7.5.3 @integration
  Scenario: The project's file is the organisation's document
    Given I have uploaded the policy "Access control policy" version "v1" with the content "Policy text v1"
    And I link the document "Access control policy" to "I.2(d)"
    When I download the evidence "Access control policy" of "Acme Portal"
    Then the file has the content "Policy text v1"

  @US-136 @iso27001-7.5.3 @integration
  Scenario: Evidence from the organisation is changed on the Documents page only
    Given I have uploaded the policy "Access control policy" version "v1" with the content "Policy text v1"
    And I link the document "Access control policy" to "I.2(d)"
    When I try to remove the evidence "Access control policy" from "Acme Portal"
    Then I see the error "This evidence comes from the organisation's documents; change it on the Documents page"
    And "Acme Portal" has the evidence "Access control policy" for "I.2(d)" from the organisation

  @US-136 @iso27001-7.5.2 @integration
  Scenario: A new version replaces the old one and the old one is kept
    Given I have uploaded the policy "Access control policy" version "v1" with the content "Policy text v1"
    And I link the document "Access control policy" to "I.2(d)"
    When I upload version "v2" of "Access control policy" with the content "Policy text v2"
    And I download the evidence "Access control policy" of "Acme Portal"
    Then the file has the content "Policy text v2"
    And the document "Access control policy" shows the earlier version "v1"

  @US-136 @iso27001-7.5.2 @integration
  Scenario: A policy due for review needs attention
    Given the time is "2026-10-02T09:00:00Z"
    And I have uploaded the policy "Access control policy" version "v1" due for review on "2026-10-01"
    Then the Documents page says "Access control policy is due for review"
    And "Needs attention" of "Acme Portal" shows "Access control policy is due for review"

  @US-136 @iso27001-7.5.3 @integration
  Scenario: Removing a document says which requirements lose it, and takes it off the projects
    Given I have uploaded the policy "Access control policy" version "v1" with the content "Policy text v1"
    And I link the document "Access control policy" to "I.2(d)"
    Then removing "Access control policy" asks to confirm that "I.2(d)" loses it
    When I remove the document "Access control policy"
    Then "Acme Portal" has no evidence "Access control policy"
    And the activity log for "Acme ApS" has "OrganisationDocumentRemoved" for "Anna"

  @US-136 @iso27001-A.5.1 @integration
  Scenario: A project that starts following the framework gets the document
    Given the project "Acme Shop" exists
    And I have uploaded the policy "Access control policy" version "v1" with the content "Policy text v1"
    And I link the document "Access control policy" to "I.2(d)"
    When I add "CRA – Regulation (EU) 2024/2847" to the project "Acme Shop"
    Then "Acme Shop" has the evidence "Access control policy" for "I.2(d)" from the organisation

  @US-136 @integration
  Scenario: An unlinked document leaves the projects
    Given I have uploaded the policy "Access control policy" version "v1" with the content "Policy text v1"
    And I link the document "Access control policy" to "I.2(d)"
    When I unlink the document "Access control policy" from "I.2(d)"
    Then "Acme Portal" has no evidence "Access control policy"

  @US-136 @iso27001-A.5.15 @integration
  Scenario: A developer cannot add documents
    Given "Carl" has joined "Acme ApS" as "Developer"
    And I am signed in as "Carl"
    When I upload the policy "Access control policy" version "v1" for the organisation
    Then I see the error "Your role does not allow this"
