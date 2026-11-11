Feature: One thread from risk to proof

  # US-146: every evidence card shows the risks it helps reduce, the requirements it proves and the stories it shows are
  # done, in that order and with how each is connected. Evidence can be linked to a user story, the AI suggests stories
  # for evidence, and a locked management report can be used as evidence.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-146 @iso27001-8.3 @integration
  Scenario: An evidence card shows the risks, requirements and stories it belongs to
    Given the project "Acme Portal" has the user story "[US-061] Secure defaults"
    And the user story "[US-061] Secure defaults" is linked to the requirement "I.2(b)"
    And the risk "No secure default config" exists in "Acme Portal"
    And the risk "No secure default config" has the requirement "I.2(b)" as a measure
    And the evidence "Hardening guide" proves "I.2(b)"
    When I open the evidence "Hardening guide"
    Then its trace shows the risk "No secure default config" "via I.2(b)"
    And its trace shows the requirement "I.2(b)"
    And its trace shows the story "[US-061] Secure defaults" "via I.2(b)"

  @US-146 @cra-annexVII @integration
  Scenario: Evidence is linked to a user story
    Given the project "Acme Portal" has the user story "[US-004] Sign in with Keycloak"
    And the evidence "Sign-in tests" is on the Evidence tab of "Acme Portal"
    When I link the evidence "Sign-in tests" to "[US-004] Sign in with Keycloak"
    Then the story "[US-004] Sign in with Keycloak" shows the evidence "Sign-in tests"
    And the activity log has "EvidenceLinkedToStory" saying "[US-004] Sign in with Keycloak: Sign-in tests"

  @US-146 @aiact-art14 @integration
  Scenario: The AI suggests a story for a piece of evidence, and a person approves it
    Given the project "Acme Portal" has the user story "[US-127] Reporting deadlines"
    And the AI rates "Report procedure" against "[US-127]" with 0.74
    When I upload the evidence "Report procedure" with the file "report.pdf" on the Evidence tab of "Acme Portal"
    Then "[US-127] Reporting deadlines" waits for my decision on "Report procedure"
    When I approve the story "[US-127] Reporting deadlines" for "Report procedure"
    Then its trace shows the story "[US-127] Reporting deadlines" "AI suggestion approved"

  @US-146 @iso27001-9.3 @integration
  Scenario: A locked management report is used as evidence
    Given I have locked the traceability report of "Acme Portal" for "management"
    When I use the locked report as evidence for "I.2(d)"
    Then the evidence "Traceability – Acme Portal (Management)" proves "I.2(d)"
