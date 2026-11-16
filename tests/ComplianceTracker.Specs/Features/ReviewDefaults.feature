Feature: Asked for a review date or validity, with a default

  # US-195: every form with a review date or validity fills in today plus the organisation's default; leaving it empty is
  # allowed but warned about and listed under "To review"; the organisation admin sets the defaults.

  Background:
    Given an empty database
    And the time is "2026-11-15T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-195 @iso27001-7.5.3 @integration
  Scenario: The evidence form fills in the default validity
    When I open the Evidence tab of "Acme Portal"
    Then the field "evidence-valid-until" holds "2027-11-15"

  @US-195 @iso27001-7.5.3 @integration
  Scenario: Evidence without a date is warned about and listed under To review
    When I add the evidence "Old policy" with the link "https://example.com/old" to "I.2(d)"
    Then I am warned "No review date: it will never remind anyone"
    And "To review" lists 1 item without a review date for "Acme Portal"

  @US-195 @iso27001-5.1 @integration
  Scenario: The organisation admin changes a default
    When I set the default evidence validity to 24 months
    And I open the Evidence tab of "Acme Portal"
    Then the field "evidence-valid-until" holds "2028-11-15"
    And the activity log has "ReviewDefaultsChanged" saying "Evidence 24"

  @US-195 @integration
  Scenario: A review date in the past is refused
    When I upload the policy "Access control policy" version "v1" due for review on "2026-01-01"
    Then I see the error "The review date cannot be in the past"

  @US-195 @iso27001-9.2 @integration
  Scenario: The audit check counts evidence without an end date
    Given I add the evidence "Old policy" with the link "https://example.com/old" to "I.2(d)"
    When I check the audit package of "Acme Portal"
    Then the check says "Evidence has an end date" is missing
