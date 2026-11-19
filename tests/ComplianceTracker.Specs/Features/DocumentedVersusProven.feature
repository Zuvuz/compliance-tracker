Feature: Documented is not the same as proven

  # US-204: a policy or a procedure describes how we work; proof shows that it happened. A requirement with only
  # documentation is "Documented – proof missing", unless documentation is enough for it (R5, R8).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And the project "Acme Portal" exists
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And I have uploaded the policy "Incident handling procedure" version "v1" with the content "How we handle incidents"

  @US-204 @iso27001-9.1 @nis2-art21 @integration
  Scenario: A procedure alone documents a requirement but does not prove it
    Given I link the document "Incident handling procedure" to "I.2(d)"
    When I open the address of "I.2(d)" in "Acme Portal"
    Then the requirement shows "Documented – proof missing"
    And "Documented by" lists "Incident handling procedure"
    And the step for the requirement's evidence asks to "Add proof"

  @US-204 @iso27001-9.1 @integration
  Scenario: Proof makes it proven, with the documentation next to it
    Given I link the document "Incident handling procedure" to "I.2(d)"
    And I have added the evidence "Report to CSIRT" with the file "report.pdf" to "I.2(d)"
    When I open the address of "I.2(d)" in "Acme Portal"
    Then the requirement shows "Proven"
    And "Proven by" lists "Report to CSIRT"
    And "Documented by" lists "Incident handling procedure"

  @US-204 @iso27001-7.5 @integration
  Scenario: Documentation is enough for a requirement that asks for the document itself
    Given I link the document "Incident handling procedure" to "I.2(d)"
    When I mark "I.2(d)" of "Acme Portal" as "documentation is enough" with the reason "The requirement asks for the procedure"
    Then the requirement shows "Proven"
    And the requirement says documentation is enough because "The requirement asks for the procedure"
    And the activity log has "DocumentationEnoughDecided" saying "I.2(d): documentation is enough (The requirement asks for the procedure)"

  @US-204 @cra-annex1 @integration
  Scenario: A built-in requirement that asks for a policy comes marked, and the mark can be removed
    Given I link the document "Incident handling procedure" to "II.5"
    When I open the address of "II.5" in "Acme Portal"
    Then the requirement shows "Proven"
    And the requirement says documentation is enough because "The requirement asks for the document itself (built in)"
    When I mark "II.5" of "Acme Portal" as "proof is needed" with the reason "We want to see the policy in use"
    Then the requirement shows "Documented – proof missing"

  @US-204 @integration
  Scenario: A person marks a piece of evidence as documentation
    Given I upload the evidence "Threat model" with the file "threat-model.pdf" on the Evidence tab of "Acme Portal"
    When I mark the evidence "Threat model" of "Acme Portal" as documentation
    Then the evidence "Threat model" of "Acme Portal" is "Documentation"
    And the activity log has "EvidenceKindChanged" saying "Threat model: Proof → Documentation"

  @US-204 @integration
  Scenario: The Documents page says what using a document as evidence does
    Given the minimum score for evidence suggestions is 0.5
    And the AI rates "Access control policy" against "I.2(d)" with 0.82
    When I upload the policy "Access control policy" version "v1" for the organisation
    Then the Documents page heads the AI's suggestions "Could be evidence for" and offers "Use as evidence"
