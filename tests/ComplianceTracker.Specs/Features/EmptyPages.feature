Feature: Empty pages that say what to do

  # US-172: an empty list says what belongs there and why, in one sentence, and offers the first step(s) the member may
  # take. A member who may not take them sees who can.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-172 @integration
  Scenario: An empty risk register says why and offers the first steps
    When I open the "Risks" tab of "Acme Portal"
    Then the empty list "no-risks" says "No risks yet" and why it matters
    And it offers "Add a risk", "Draft risks with AI" and "Import from a file"

  @US-172 @integration
  Scenario: A member who may not add sees who can
    Given "Bo" is in "Acme ApS" with the role "Auditor"
    When "Bo" opens the "Risks" tab of "Acme Portal"
    Then the empty list "no-risks" offers nothing and says "A member who manages risks adds them"

  @US-172 @integration
  Scenario: Other empty lists say why too
    When I open the "Scans" tab of "Acme Portal"
    Then the empty list "no-scans" says "No scans yet" and why it matters
    And the empty list "no-sbom" says "No SBOM yet" and why it matters
