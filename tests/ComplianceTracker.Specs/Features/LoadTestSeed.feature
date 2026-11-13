Feature: The load test's data

  # US-138: the load test runs against a realistic organisation, seeded straight into an empty test database by
  # tests/load/seed. The seed keeps the domain's rules (the log stays a valid hash chain) and never touches a database
  # that already has data (R6). The load test itself (k6) runs from its own workflow, started by hand.

  Background:
    Given an empty database

  @US-138 @iso27001-A.8.6 @integration
  Scenario: The seed makes a realistic organisation the users can open
    When the load test's data is seeded for "Lea", "Max" and "Nor"
    Then the organisation "Load Test ApS" has 5 projects, 1000 stories, 300 pieces of evidence and 200 risks
    And the organisation's activity log has 5000 entries and its chain is intact
    And "Max" sees the project "Product 3" on the projects page

  @US-138 @integration
  Scenario: The seed never touches a database with data
    Given "Anna" has created the organisation "Acme ApS"
    When the load test's data is seeded for "Lea"
    Then the seed is refused with "The database already has organisations; the load test seeds an empty test database only"
