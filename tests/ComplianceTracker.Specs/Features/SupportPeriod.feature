Feature: Support period

  # US-025: a released product has a support end date (CRA Art. 13(8)), shown to users (Art. 13(19)). Under five years
  # needs a reason; the date can only move later; every change is logged.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the time is "2026-11-01T09:00:00Z"

  @US-025 @cra-art13 @integration
  Scenario: Release requires a support end date
    When I change the phase of "Acme Portal" to "Released"
    Then I see the error "Set the support end date before release"

  @US-025 @cra-art13 @integration
  Scenario: A short support period needs a reason
    When I release "Acme Portal" with support until "2029-11-01" and no reason
    Then I see the error "A support period under five years needs a reason"

  @US-025 @cra-art13 @integration
  Scenario: A short support period with a reason is accepted
    When I release "Acme Portal" with support until "2029-11-01" and the reason "A campaign site, used for three years"
    Then the header of "Acme Portal" shows "Supported until 01-11-2029"

  @US-025 @cra-art13 @integration
  Scenario: The support end date is shown
    When I release "Acme Portal" with support until "2031-11-01"
    Then the header of "Acme Portal" shows "Supported until 01-11-2031"
    And the page of "Acme Portal" shows phase "Released"

  @US-025 @integration
  Scenario: The support end date cannot be moved earlier
    Given "Acme Portal" is released with support until "2031-11-01"
    When I set the support end date of "Acme Portal" to "2030-11-01"
    Then I see the error "The support end date cannot be moved earlier"

  @US-025 @integration
  Scenario: The support end date can be moved later
    Given "Acme Portal" is released with support until "2031-11-01"
    When I set the support end date of "Acme Portal" to "2032-11-01"
    Then the header of "Acme Portal" shows "Supported until 01-11-2032"

  @US-025 @iso27001-A.8.15 @integration
  Scenario: The support period is logged
    When I release "Acme Portal" with support until "2031-11-01"
    Then the activity log for "Acme ApS" has "SupportPeriodSet" for "Anna" concerning "Acme Portal"
    And the "SupportPeriodSet" entry says "Support until: (empty) → 01-11-2031"

  @US-025 @cra-annex7 @integration
  Scenario: The technical documentation shows the support period
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Acme Portal" is released with support until "2031-11-01"
    When I make the technical documentation draft of "Acme Portal"
    Then the section "General description of the product" says "Supported until 01-11-2031"
