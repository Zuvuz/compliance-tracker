Feature: Evidence per requirement

  # US-057: a requirement of a project can get proof, a link or a file, so an auditor sees that it is met
  # and not only that a user story is linked to it. In the MVP evidence belongs to the requirement (B33).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"
    And the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"

  @US-057 @iso27001-7.5 @integration
  Scenario: Add a link as evidence
    When I add the evidence "PR 42" with the link "https://github.com/acme/portal/pull/42" to "I.2(d)"
    Then the row "I.2(d)" shows the evidence "PR 42"
    And the summary shows 1 with evidence
    And the activity log for "Acme ApS" has "EvidenceAdded" for "Anna" concerning "Acme Portal"

  @US-057 @iso27001-7.5 @integration
  Scenario: Add a file as evidence
    When I add the evidence "Pentest report" with the file "pentest.pdf" to "I.2(d)"
    Then the row "I.2(d)" shows the evidence "Pentest report" with its SHA-256
    And the file of "Pentest report" can be downloaded unchanged

  @US-057 @integration
  Scenario: Covered without evidence
    When I open the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" is "Covered" without evidence
    And the summary shows 0 with evidence

  @US-057 @integration
  Scenario: Expired evidence does not count
    Given the time is "2026-11-03T10:00:00Z"
    When I add the evidence "Pentest report" with the link "https://example.com/pentest" valid until "01-10-2026" to "I.2(d)"
    Then the row "I.2(d)" shows the evidence "Pentest report" as "Expired"
    And the summary shows 0 with evidence

  @US-057 @iso27001-A.8.12 @integration
  Scenario: A too large file is refused
    When I add the evidence "Dump" with a file of 11 MB to "I.2(d)"
    Then I see the error "The file is larger than 10 MB"
    And no evidence is stored

  @US-057 @iso27001-A.8.28 @integration
  Scenario: A link must be a web address
    When I add the evidence "Script" with the link "javascript:alert(1)" to "I.2(d)"
    Then I see the error "The link must start with http:// or https://"

  @US-057 @iso27001-A.8.15 @integration
  Scenario: Removed evidence is kept as history
    Given I have added the evidence "PR 42" with the link "https://github.com/acme/portal/pull/42" to "I.2(d)"
    When I remove the evidence "PR 42" from "I.2(d)"
    Then the row "I.2(d)" is "Covered" without evidence
    And the removed evidence "PR 42" is still stored
    And the activity log for "Acme ApS" has "EvidenceRemoved" for "Anna" concerning "Acme Portal"

  @US-057 @iso27001-A.5.15 @integration
  Scenario: An auditor sees evidence but cannot add it
    Given I have added the evidence "PR 42" with the link "https://github.com/acme/portal/pull/42" to "I.2(d)"
    And "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the "CRA" sub-tab of "Acme Portal"
    Then the row "I.2(d)" shows the evidence "PR 42"
    And the row "I.2(d)" has no "+ Evidence" link

  @US-057 @iso27001-A.5.15 @integration
  Scenario: Another organisation cannot download the file
    Given I have added the evidence "Pentest report" with the file "pentest.pdf" to "I.2(d)"
    And "Carl" has created the organisation "Beta ApS"
    When "Carl" downloads the file of "Pentest report"
    Then the page is not found
