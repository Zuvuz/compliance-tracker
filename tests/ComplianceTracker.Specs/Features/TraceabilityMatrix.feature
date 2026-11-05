Feature: Traceability matrix

  # US-052: each requirement and the user stories that cover it, as CSV for an auditor's own tools.
  # Part of the MVP (B33). Also lists the evidence on each requirement (US-057).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"

  @US-052 @cra-annex7 @integration
  Scenario: Each requirement and its stories
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    When I download the traceability matrix of "Acme Portal" as CSV
    Then the line for "I.2(d)" has the status "Covered" and the story "[US-004] Sign in with Keycloak"
    And the line for "I.2(a)" has the status "Not covered" and no story
    And the line for "I.2(d)" says it was linked by "Anna" with the source "Manual"

  @US-052 @iso27001-5.31 @integration
  Scenario: Requirements that do not apply have their reason
    Given "I.2(k)" is not applicable for "Acme Portal"
    When I download the traceability matrix of "Acme Portal" as CSV
    Then the line for "I.2(k)" has the status "Not applicable" and a reason

  @US-052 @integration
  Scenario: Removed links are not in the matrix
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    And the link between "[US-004] Sign in with Keycloak" and "I.2(d)" is removed
    When I download the traceability matrix of "Acme Portal" as CSV
    Then the line for "I.2(d)" has the status "Not covered" and no story

  @US-052 @US-057 @iso27001-7.5 @integration
  Scenario: The evidence is in the matrix
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    And I have added the evidence "PR 42" with the link "https://github.com/acme/portal/pull/42" to "I.2(d)"
    When I download the traceability matrix of "Acme Portal" as CSV
    Then the line for "I.2(d)" has the evidence "PR 42 (https://github.com/acme/portal/pull/42)"

  @US-052 @integration
  Scenario: One regulation at a time
    Given the framework "Security policy" has the active requirements "SP-1 Passwords" and "SP-2 Backups"
    And "Acme Portal" uses "Security policy" version 1
    When I download the traceability matrix of the "CRA" sub-tab of "Acme Portal"
    Then every line is about "CRA – Regulation (EU) 2024/2847"

  @US-052 @iso27001-A.8.28 @integration
  Scenario: A formula in a story title is not run
    Given the project "Acme Portal" has the user story '=HYPERLINK("http://evil")'
    And the user story '=HYPERLINK("http://evil")' is linked to the requirement "I.2(a)"
    When I download the traceability matrix of "Acme Portal" as CSV
    Then no cell starts with "="

  @US-052 @iso27001-A.5.15 @integration
  Scenario: An auditor can download it
    Given "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" downloads the traceability matrix of "Acme Portal" as CSV
    Then the file has a header line

  @US-052 @iso27001-A.5.15 @integration
  Scenario: Another organisation cannot download it
    Given "Carl" has created the organisation "Beta ApS"
    When "Carl" downloads the traceability matrix of "Acme Portal" as CSV
    Then the page is not found
