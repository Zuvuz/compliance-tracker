Feature: The organisation's own checklist items

  # US-192: the organisation adds its own items to the lifecycle checklists, for a phase, from its own policy or tied to one
  # of its frameworks. They are shown with their source and ticked like the built-in items.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-192 @iso27001-A.8.25 @integration
  Scenario: An own item is shown in every project in its phase
    Given I have added the checklist item "Architecture review held" to the phase "Design" with the reference "ISMS-PRO-12"
    And "Acme Portal" is in the phase "Design"
    When I open the overview of "Acme Portal"
    Then the checklist lists "Architecture review held" for "Own policy ISMS-PRO-12"
    And the activity log has "OrganisationChecklistItemAdded" saying "Design: Architecture review held (Own policy)"

  @US-192 @iso27001-A.8.25 @integration
  Scenario: An own item is ticked like a built-in one and counts in the phase warning
    Given I have added the checklist item "Architecture review held" to the phase "Design" with the reference ""
    And "Acme Portal" is in the phase "Design"
    When I open the overview of "Acme Portal"
    Then the phase form warns that "Architecture review held" is missing
    When I tick the checklist item "Architecture review held" of "Acme Portal" with the comment "Held 12-11"
    Then the checklist item "Architecture review held" is "Done"

  @US-192 @integration
  Scenario: An item tied to a framework is shown only where the framework is followed
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And I have added the checklist item "Vulnerability triage rota set" to the phase "Released" for "CRA"
    And "Acme Portal" is in the phase "Released"
    When I open the overview of "Acme Portal"
    Then the checklist does not list "Vulnerability triage rota set"

  @US-192 @integration
  Scenario: A removed item is no longer shown
    Given I have added the checklist item "Architecture review held" to the phase "Design" with the reference ""
    And I have removed the checklist item "Architecture review held"
    And "Acme Portal" is in the phase "Design"
    When I open the overview of "Acme Portal"
    Then the checklist does not list "Architecture review held"

  @US-192 @iso27001-A.5.15 @integration
  Scenario: A developer cannot add checklist items
    Given "Carl" has joined "Acme ApS" as "Developer"
    And I am signed in as "Carl"
    When I open the organisation's checklists
    Then there is no form to add a checklist item
