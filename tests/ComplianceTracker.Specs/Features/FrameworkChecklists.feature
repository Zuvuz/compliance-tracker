Feature: Lifecycle checklists for frameworks the tracker does not know in advance

  # US-193: for a framework the organisation uploaded, the AI suggests which requirements are lifecycle activities and in
  # which phase. A person approves (with the text and phase they choose) or rejects; an approved item is shown in the
  # projects following the framework, with the framework and the requirement's code as its source.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the minimum score for evidence suggestions is 0.5
    And the framework "Customer requirements" has the active requirements "SR-12 Penetration testing before each release" and "SR-13 Office opening hours"
    And the AI rates "Verification" against "SR-12" with 0.9

  @US-193 @iso27001-A.5.31 @integration
  Scenario: The AI suggests a phase for a lifecycle activity and leaves the rest out
    When I ask the AI for checklist items for "Customer requirements"
    Then the AI suggests the checklist item "SR-12" for the phase "Verification"
    And the AI does not suggest a checklist item for "SR-13"

  @US-193 @iso27001-A.8.25 @ai-act-art14 @integration
  Scenario: An approved suggestion becomes a checklist item in the projects following the framework
    Given I have asked the AI for checklist items for "Customer requirements"
    And I have approved the checklist suggestion "SR-12" as "Penetration test done"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Customer requirements"
    And "Acme Portal" is in the phase "Verification"
    When I open the overview of "Acme Portal"
    Then the checklist lists "Penetration test done" for "Customer requirements SR-12"

  @US-193 @integration
  Scenario: A rejected suggestion is not suggested again
    Given I have asked the AI for checklist items for "Customer requirements"
    And I have rejected the checklist suggestion "SR-12"
    When I ask the AI for checklist items for "Customer requirements"
    Then the AI does not suggest a checklist item for "SR-12"

  @US-193 @integration
  Scenario: A new version without the requirement removes its item
    Given I have asked the AI for checklist items for "Customer requirements"
    And I have approved the checklist suggestion "SR-12" as "Penetration test done"
    And version 2 of "Customer requirements" has the requirement "SR-14 Yearly review"
    When I ask the AI for checklist items for "Customer requirements"
    Then the organisation's checklists do not list "Penetration test done"
