Feature: CRA technical documentation draft

  # US-056: a DOCX draft of the CRA technical documentation for a project, with the headings of Annex VII, made from
  # the tracker's data. What the tracker cannot fill says "To be completed". Locking a final version comes with US-050.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"

  @US-056 @cra-annex7 @integration
  Scenario: The draft follows Annex VII
    When I make the technical documentation draft of "Acme Portal"
    Then the draft has the headings of CRA Annex VII
    And the draft says it was made by "Anna"

  @US-056 @cra-annex7 @integration
  Scenario: The essential requirements show how they are met
    Given the user story "[US-004] Sign in with Keycloak" is linked to the requirement "I.2(d)"
    And I mark "I.2(k)" as not applicable for "Acme Portal" with the reason "No network-facing interface"
    When I make the technical documentation draft of "Acme Portal"
    Then the draft says "I.2(d)" is met by "[US-004] Sign in with Keycloak"
    And the draft says "I.2(k)" does not apply because "No network-facing interface"

  @US-056 @cra-art13 @integration
  Scenario: The risk assessment comes from the risk register
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    When I make the technical documentation draft of "Acme Portal"
    Then the section "Cybersecurity risk assessment" says "Stolen session cookie"

  @US-056 @cra-annex7 @integration
  Scenario: What the tracker cannot fill is marked
    When I make the technical documentation draft of "Acme Portal"
    Then the section "Software bill of materials" says "To be completed"
    And the section "EU declaration of conformity" says "To be completed"

  @US-056 @integration
  Scenario: Only for projects that follow the CRA
    Given the project "Beta App" exists
    When I open the reports of "Beta App"
    Then the "Technical documentation (draft)" report cannot be made because "Add the CRA to the project to make the technical documentation draft"
    When I ask for the technical documentation draft of "Beta App" directly
    Then the page is not found
