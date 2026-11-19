Feature: Definition of Done for user stories and use cases

  # US-109: a project has a Definition of Done per kind of work item. The tracker checks what it can from its own data,
  # a person ticks the rest, and the audit shows which finished work items meet it, also as CSV for the auditor.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-109 @iso27001-A.8.25 @integration
  Scenario: A finished user story that misses a criterion is shown in the audit
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "I.2(d)"
    And the user story "[US-004] Sign in" is closed
    When I open the Definition of Done audit of "Acme Portal"
    Then "[US-004] Sign in" does not meet the Definition of Done
    And "[US-004] Sign in" misses "Every linked requirement has evidence" because "I.2(d): no evidence"
    And "[US-004] Sign in" misses "Code reviewed" because "Not ticked"
    And "[US-004] Sign in" meets "Linked to at least one requirement"

  @US-109 @iso27001-7.5.3 @integration
  Scenario: Ticking a manual criterion keeps who and when
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    When I tick "Code reviewed" on "[US-004] Sign in" in "Acme Portal"
    Then "[US-004] Sign in" meets "Code reviewed" with a note starting "Ticked by Anna on"
    And the activity log has "DoneCriterionTicked" with "[US-004] Sign in: Code reviewed"

  @US-109 @iso27001-A.8.25 @integration
  Scenario: A use case is finished when it is marked Done
    Given I have chosen the methods "Use cases" for "Acme Portal"
    And I have uploaded the use case document "use-cases.docx" to "Acme Portal" containing:
      """
      # UC-12 Sign in as a radiologist
      Primary actor: Radiologist
      Main success scenario:
      1. The radiologist opens the tool
      2. The radiologist enters the one-time code
      """
    When I mark "Sign in as a radiologist" of "Acme Portal" as Done
    Then "Sign in as a radiologist" does not meet the Definition of Done
    And "Sign in as a radiologist" misses "Primary actor, main success scenario and extensions filled in" because "Empty: extensions"

  @US-109 @iso27001-9.2 @integration
  Scenario: The auditor exports the audit
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is closed
    And "Ida" has joined "Acme ApS" as "Auditor"
    And I am signed in as "Ida"
    When I export the Definition of Done audit of "Acme Portal" as CSV
    Then the export has a line for "[US-004] Sign in" and "Code reviewed" with the status "NotMet"

  @US-109 @integration
  Scenario: Finished work that misses the Definition of Done needs attention
    Given the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is closed
    When I open the overview of "Acme Portal"
    Then "Needs attention" says "1 finished work item does not meet the Definition of Done"

  @US-109 @iso27001-A.8.25 @integration
  Scenario: Changing the Definition of Done is logged with the old and new criteria
    When I change the user stories' manual criteria of "Acme Portal" to "Code reviewed" and "Security reviewed"
    Then the user stories' criteria of "Acme Portal" end with "Code reviewed, Security reviewed"
    And the activity log has "DefinitionOfDoneChanged" with "Tests passed, Code reviewed → Rules and examples filled in, Linked to at least one requirement, Every linked requirement has evidence, Tests passed, Code reviewed, Security reviewed (version 1)"
