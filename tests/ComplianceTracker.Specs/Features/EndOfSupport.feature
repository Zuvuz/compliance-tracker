Feature: End of support and retention

  # US-074: when a product reaches end of support, the project is closed and kept, with its documentation, for at least
  # 10 years after the product was last placed on the market, or until the end of the support period if that is later
  # (CRA Art. 13(13)). A later date may be chosen, never an earlier one.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And the time is "2026-11-01T09:00:00Z"
    And "Acme Portal" is released with support until "2031-12-31"

  @US-074 @cra-art13 @integration
  Scenario: The retention date is set at end of support
    Given the time is "2032-01-10T09:00:00Z"
    When I end the support of "Acme Portal", last placed on the market "2026-12-31"
    Then the page of "Acme Portal" says "End of support since 10-01-2032 · kept until 31-12-2036"

  @US-074 @cra-art13 @integration
  Scenario: A support period longer than ten years keeps the project longer
    Given the time is "2027-01-10T09:00:00Z"
    And I set the support end date of "Acme Portal" to "2040-06-30"
    When I end the support of "Acme Portal", last placed on the market "2027-01-01"
    Then the page of "Acme Portal" says "End of support since 10-01-2027 · kept until 30-06-2040"

  @US-074 @cra-art13 @integration
  Scenario: The retention date cannot be earlier than the rules require
    Given the time is "2032-01-10T09:00:00Z"
    When I end the support of "Acme Portal", last placed on the market "2026-12-31", and keep it until "2035-01-01"
    Then I see the error "The project must be kept until at least 31-12-2036"
    And the page of "Acme Portal" shows phase "Released"

  @US-074 @cra-art13 @integration
  Scenario: A later retention date can be chosen
    Given the time is "2032-01-10T09:00:00Z"
    When I end the support of "Acme Portal", last placed on the market "2026-12-31", and keep it until "2040-01-01"
    Then the page of "Acme Portal" says "End of support since 10-01-2032 · kept until 01-01-2040"

  @US-074 @iso27001-A.8.15 @integration
  Scenario: End of support is logged with the retention date
    Given the time is "2032-01-10T09:00:00Z"
    When I end the support of "Acme Portal", last placed on the market "2026-12-31"
    Then the "ProjectPhaseChanged" entry says "Phase: Released → EndOfSupport, kept until 31-12-2036 (approved by Approver)"

  @US-074 @iso27001-A.5.33 @integration
  Scenario: The closed project can still be read by the Auditor
    Given "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And "Ida" has joined "Acme ApS" as "Auditor"
    And the time is "2032-01-10T09:00:00Z"
    And I end the support of "Acme Portal", last placed on the market "2026-12-31"
    And the time is "2033-03-01T09:00:00Z"
    When "Ida" downloads the traceability matrix of "Acme Portal" as CSV
    Then the line for "I.2(d)" has the status "Not covered" and no story
