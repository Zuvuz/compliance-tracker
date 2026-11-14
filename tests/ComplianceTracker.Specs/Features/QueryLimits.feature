Feature: Each page has a limit on database queries

  # US-139: the important pages make a fixed number of database queries, whatever the amount of data, so a page that
  # would get slow as the data grows (N+1 queries) is caught here before it reaches a customer. A limit is only raised
  # in the same pull request as the change that needs it, with the reason (R4).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @US-139 @iso27001-A.8.6 @integration
  Scenario: The pages do not make more queries with more data
    Given "Acme Portal" has 5 user stories, 3 risks and 3 pieces of evidence on its requirements
    And I count the queries of the important pages
    When "Acme Portal" gets 200 user stories, 60 risks and 100 pieces of evidence on its requirements
    And the organisation gets 15 more projects that use the same framework
    # The limits are just above what the pages made on 12-11-2026 (B96).
    # US-155: the traceability download reads the "needs action" marks too (one query): 50 -> 53.
    Then each page stays within its limit and does not grow with the data:
      | Page                  | Limit |
      | Dashboard             | 12    |
      | Projects              | 12    |
      | Overview              | 52    |
      | Compliance            | 54    |
      | Evidence              | 35    |
      | Risks                 | 13    |
      | Work items            | 30    |
      | Reports               | 30    |
      | Project activity      | 14    |
      | Frameworks            | 5     |
      | Organisation activity | 8     |
      | Traceability download | 53    |
