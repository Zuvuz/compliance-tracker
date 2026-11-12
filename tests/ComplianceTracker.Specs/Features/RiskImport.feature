Feature: Import risks from a spreadsheet

  # US-049: an existing risk register comes in from a CSV file or an Excel workbook. Every row is checked and shown in a
  # preview first; one bad row stops the whole import. Duplicates are skipped unless chosen, and the import is logged once.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-049 @iso27001-6.1.2 @integration
  Scenario: Import risks from the template
    When I import the risks file "risks.csv" into "Acme Portal" with the rows
      | Title                 | Likelihood | Impact | Owner | Treatment | Reason        |
      | Stolen session cookie | 3          | 4      | Anna  | Reduce    |               |
      | Old TLS on legacy API | 2          | 3      | Anna  | Accept    | Retired in Q1 |
    And I confirm the import
    Then the risk "Stolen session cookie" has the level "High"
    And the risk "Old TLS on legacy API" has the status "Accepted"
    And the activity log has "RisksImported" saying "Imported 2 risks from risks.csv"

  @US-049 @integration
  Scenario: One bad row stops the whole import
    When I import the risks file "risks.csv" into "Acme Portal" with the rows
      | Title                 | Likelihood | Impact | Owner | Treatment | Reason |
      | Stolen session cookie | 3          | 4      | Anna  | Reduce    |        |
      | Broken row            | 6          | 3      | Anna  | Reduce    |        |
    Then the preview says "Row 2: Likelihood must be 1 to 5"
    And the import cannot be confirmed
    And "Acme Portal" has no risks

  @US-049 @integration
  Scenario: A duplicate is skipped
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    When I import the risks file "risks.csv" into "Acme Portal" with the rows
      | Title                 | Likelihood | Impact | Owner | Treatment | Reason |
      | Stolen session cookie | 3          | 4      | Anna  | Reduce    |        |
      | Phishing              | 2          | 2      | Anna  | Reduce    |        |
    Then the preview marks "Stolen session cookie" as "Already in the project"
    When I confirm the import
    Then "Acme Portal" has 1 risk named "Stolen session cookie"
    And the activity log has "RisksImported" saying "Imported 1 risk from risks.csv; 1 already in the project skipped"

  @US-049 @integration
  Scenario: A duplicate can be imported anyway
    Given the risk "Stolen session cookie" exists in "Acme Portal"
    When I import the risks file "risks.csv" into "Acme Portal" with the rows
      | Title                 | Likelihood | Impact |
      | Stolen session cookie | 3          | 4      |
    And I confirm the import and import the duplicates anyway
    Then "Acme Portal" has 2 risks named "Stolen session cookie"

  @US-049 @integration
  Scenario: An owner who is not a member is kept as text
    When I import the risks file "risks.csv" into "Acme Portal" with the rows
      | Title    | Likelihood | Impact | Owner |
      | Phishing | 2          | 2      | Carl  |
    Then the preview shows the owner of "Phishing" as "Carl (not a member)"
    When I confirm the import
    Then the risk "Phishing" says it was imported from "risks.csv" with the owner "Carl"

  @US-049 @integration
  Scenario: A file with commas is read like one with semicolons
    When I import the comma-separated risks file "risks.csv" into "Acme Portal" with the rows
      | Title                 | Description             | Likelihood | Impact |
      | Stolen session cookie | Stolen via XSS, or MITM | 3          | 4      |
    And I confirm the import
    Then the risk "Stolen session cookie" has the level "High"

  @US-049 @integration
  Scenario: An Excel workbook is read
    When I import the Excel risks file "risks.xlsx" into "Acme Portal" with the rows
      | Title    | Likelihood | Impact | Last reviewed |
      | Phishing | 2          | 2      | 2026-05-01    |
    And I confirm the import
    Then the risk "Phishing" has the level "Low"

  @US-049 @iso27001-A.5.15 @integration
  Scenario: An auditor cannot import
    Given "Bo" has joined "Acme ApS" as "Auditor"
    When "Bo" opens the risk import of "Acme Portal"
    Then access is denied

  @US-049 @integration
  Scenario: A high risk to accept still needs a second person
    When I import the risks file "risks.csv" into "Acme Portal" with the rows
      | Title           | Likelihood | Impact | Treatment | Reason          |
      | Legacy database | 4          | 4      | Accept    | Replaced in 2027 |
    And I confirm the import
    Then the risk "Legacy database" has the status "Open"
