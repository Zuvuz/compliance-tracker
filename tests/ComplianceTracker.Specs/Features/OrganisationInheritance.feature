Feature: Compliance for the organisation itself, inherited by its projects

  # US-115: a requirement the organisation follows is met once in the organisation area and inherited by the products
  # ("down"), or carried out in the projects, where each product proves it and the organisation gathers the proof ("up").
  # A product can opt out of an inherited requirement with a reason (R4).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And I have done the NIS2 scope check as an important entity
    And I have uploaded the policy "Information security policy" version "v1" with the content "How we keep information safe"
    And I link the document "Information security policy" to "Art. 21(2)(a)"

  @US-115 @iso27001-5.2 @nis2-art21 @integration
  Scenario: A product inherits the organisation's requirements with their status
    When I open what "Acme Portal" inherits from the organisation
    Then the inherited "Art. 21(2)(a)" of "NIS2" is "Covered" and "Inherited from the organisation"
    And the inherited "Art. 21(2)(b)" of "NIS2" is "Not covered" and "Inherited from the organisation"

  @US-115 @iso27001-4.3 @integration
  Scenario: A product outside the ISMS's scope opts out of an inherited requirement, and back in
    When I mark the inherited "Art. 21(2)(b)" as not applicable to "Acme Portal" because "Outside the ISMS's scope"
    Then the inherited "Art. 21(2)(b)" of "NIS2" is "Not covered" and "Not applicable here"
    And the activity log has "RequirementMarkedNotApplicable" saying "Art. 21(2)(b) (from the organisation): Outside the ISMS's scope"
    When I make the inherited "Art. 21(2)(b)" applicable to "Acme Portal" again
    Then the inherited "Art. 21(2)(b)" of "NIS2" is "Not covered" and "Inherited from the organisation"

  @US-115 @iso27001-A.8.25 @nis2-art21 @integration
  Scenario: A requirement carried out in the projects gathers the products' proof
    Given the project "Acme App" exists
    And I upload the evidence "SAST scan" with the file "sast.pdf" on the Evidence tab of "Acme Portal"
    When I carry out "Art. 21(2)(e)" of "NIS2" in the projects because "Secure development happens in each product"
    And I add "SAST scan" as proof of the inherited "Art. 21(2)(e)" for "Acme Portal"
    Then the organisation's Compliance tab for "NIS2" shows "Art. 21(2)(e)" as "Not covered"
    And the organisation's "Art. 21(2)(e)" of "NIS2" says "Proven in Acme Portal · No proof yet: Acme App"
    And the activity log has "RequirementScopeChanged" saying "Art. 21(2)(e): Carried out in the projects (Secure development happens in each product)"
    When I mark the inherited "Art. 21(2)(e)" as not applicable to "Acme App" because "No software is developed"
    Then the organisation's Compliance tab for "NIS2" shows "Art. 21(2)(e)" as "Covered"
    And the organisation's "Art. 21(2)(e)" of "NIS2" says "Proven in Acme Portal"
