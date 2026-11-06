Feature: Review links after a framework version change
  US-110: links, "not applicable" decisions and evidence follow the project to a new framework version (US-069).
  Only what follows a requirement whose title or text changed waits for a person to check it against the new text.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Passwords" and "SP-2 Backups"
    And the project "Acme Portal" exists
    And "Acme Portal" uses "Security policy" version 1
    And the project "Acme Portal" has the user story "[US-004] Sign in"
    And the user story "[US-004] Sign in" is linked to the requirement "SP-1"
    And the user story "[US-004] Sign in" is linked to the requirement "SP-2"
    And version 2 of "Security policy" has the requirements "SP-1 Passwords and passphrases" and "SP-2 Backups"

  @US-110 @iso27001-8.32 @integration
  Scenario: Unchanged requirements carry over without review
    When I move "Acme Portal" to the newest version of "Security policy"
    And I open the "Security policy" sub-tab of "Acme Portal"
    Then the row "SP-2" is "Covered" by "[US-004] Sign in"
    And the row "SP-2" needs no review

  @US-110 @iso27001-8.32 @integration
  Scenario: A changed requirement is marked for review
    When I move "Acme Portal" to the newest version of "Security policy"
    And I open the "Security policy" sub-tab of "Acme Portal"
    Then the row "SP-1" is "Covered, needs review" by "[US-004] Sign in"
    And the row "SP-1" shows the old text "Passwords" and the new text "Passwords and passphrases"
    And the coverage says "1 of 2 requirements covered (50 %)"
    And the activity log of "Acme Portal" says "1 to review because the requirement's text changed"

  @US-110 @iso27001-7.5.3 @integration
  Scenario: A reviewed link counts again
    When I move "Acme Portal" to the newest version of "Security policy"
    And I open the "Security policy" sub-tab of "Acme Portal"
    And I mark "SP-1" as still correct
    Then the row "SP-1" is "Covered" by "[US-004] Sign in"
    And the row "SP-1" needs no review
    And the review of "Acme Portal" is logged as "SP-1: still correct after the version change (1 checked)"

  @US-110 @integration
  Scenario: Removing a link that no longer fits
    When I move "Acme Portal" to the newest version of "Security policy"
    And I open the "Security policy" sub-tab of "Acme Portal"
    And I remove the link between "[US-004] Sign in" and "SP-1"
    Then the row "SP-1" is "Not covered"

  @US-110 @integration
  Scenario: Evidence on a changed requirement waits for review too
    Given I have added the evidence "Password rules" with the link "https://example.com/passwords" to "SP-1"
    When I move "Acme Portal" to the newest version of "Security policy"
    And I open the "Security policy" sub-tab of "Acme Portal"
    Then the evidence "Password rules" on the row "SP-1" waits for review

  @US-110 @integration
  Scenario: The Overview asks for the review
    When I move "Acme Portal" to the newest version of "Security policy"
    And "Anna" opens the "Overview" tab of "Acme Portal"
    Then "Needs attention" says "1 requirement needs review after a version change"
    And "1 requirement needs review after a version change" links to the "Security policy" sub-tab showing "Needs review"
