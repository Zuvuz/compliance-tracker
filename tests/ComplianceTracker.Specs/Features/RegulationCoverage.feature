Feature: Coverage of the built-in regulations

  # US-068: docs/daekning-af-regulativer.md lists every article and annex of each built-in regulation and whether
  # the tracker has its requirements. These checks keep the list and the built-in requirements in step.

  Background:
    Given an empty database
    And the coverage list in "docs/daekning-af-regulativer.md"

  @US-068 @iso27001-A.5.31 @integration
  Scenario: Every requirement the list marks "In the tracker" is built in
    When the built-in requirements of each regulation are read
    Then every code the list marks "I trackeren" exists

  @US-068 @iso27001-A.5.31 @integration
  Scenario: Every built-in requirement is on the list
    When the built-in requirements of each regulation are read
    Then every built-in requirement is on the list

  @US-068 @integration
  Scenario: A missing obligation is linked to an issue
    Then every line marked "Mangler" links to an issue

  @US-068 @integration
  Scenario: The list says which version of each regulation it was checked against
    Then every regulation on the list says which text and date it was checked against
