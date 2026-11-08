Feature: Check the extraction from a framework PDF

  # US-070: before a version is activated, its draft shows what the extraction did not use: text that became no
  # requirement, gaps in the numbering, table of contents entries not found, and "shall" sentences outside every
  # requirement. EU acts are read by their articles, paragraphs and points.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-070 @iso27001-A.5.31 @integration
  Scenario: Text that did not become a requirement is shown
    When I add the framework "Security policy" with a PDF of pages:
      | page | line                                  |
      | 1    | 5.1 Access control                    |
      | 1    | Access is granted by role.            |
      | 2    | Records are kept for five years.      |
    Then the extraction check of "Security policy" shows "1 requirements found · 1 of 2 pages gave none"
    And the extraction check of "Security policy" lists "Records are kept for five years." on page 2

  @US-070 @iso27001-A.8.32 @integration
  Scenario: Many pages without requirements give a warning
    When I add the framework "Security policy" with a PDF of pages:
      | page | line                     |
      | 1    | 5.1 Access control       |
      | 2    | Background of the policy |
      | 3    | History of the policy    |
      | 4    | 5.2 Passwords            |
    Then the extraction check of "Security policy" shows a warning "Check the extraction: 50 % of the pages gave no requirements"

  @US-070 @integration
  Scenario: Headers, footers and page numbers are not listed
    When I add the framework "Security policy" with a PDF of pages:
      | page | line                 |
      | 1    | Acme security policy |
      | 1    | 5.1 Access control   |
      | 1    | 1                    |
      | 2    | Acme security policy |
      | 2    | 5.2 Passwords        |
      | 2    | 2                    |
      | 3    | Acme security policy |
      | 3    | 5.3 Backups          |
      | 3    | Page 3 of 3          |
    Then the extraction check of "Security policy" shows "All text is part of a requirement or recognised as not one."
    And the extraction check of "Security policy" shows no warning

  @US-070 @iso27001-A.5.31 @integration
  Scenario: A gap in the numbering is shown
    When I add the framework "Security policy" with a PDF of pages:
      | page | line           |
      | 1    | 5.14 Logging   |
      | 1    | 5.16 Backups   |
    Then the extraction check of "Security policy" shows "5.15 may be missing (5.14 is followed by 5.16)"

  @US-070 @iso27001-A.5.31 @integration
  Scenario: A table of contents entry that was not found is shown
    When I add the framework "Security policy" with a PDF of pages:
      | page | line                           |
      | 1    | 5.1 Access control ....... 2   |
      | 1    | 5.2 Passwords ............ 3   |
      | 2    | 5.1 Access control             |
      | 2    | Access is granted by role.     |
    Then the extraction check of "Security policy" shows "5.2 is in the table of contents but was not found"

  @US-070 @iso27001-A.5.31 @integration
  Scenario: A "shall" sentence outside every requirement is shown
    When I add the framework "Security policy" with a PDF of pages:
      | page | line                                 |
      | 1    | 5.1 Access control                   |
      | 2    | The supplier shall encrypt backups.  |
    Then the extraction check of "Security policy" lists "The supplier shall encrypt backups." on page 2
    And the extraction check of "Security policy" shows "Possible missed requirements"

  @US-070 @cra-annexI @integration
  Scenario: An EU act is read by its articles, paragraphs and points
    When I add the framework "Regulation" with a PDF of pages:
      | page | line                                               |
      | 1    | (1) Whereas products shall be secure.              |
      | 2    | Article 13                                         |
      | 2    | Obligations of manufacturers                       |
      | 2    | 1. Manufacturers shall ensure products are secure. |
      | 2    | 2. Manufacturers shall assess the risks:           |
      | 2    | (a) during planning;                               |
      | 2    | (b) during design.                                 |
    Then the draft of "Regulation" version 1 lists exactly "Art. 13(1), Art. 13(2)(a), Art. 13(2)(b)"
    And the extraction check of "Regulation" does not show "Whereas products shall be secure."

  @US-070 @iso27001-A.5.31 @integration
  Scenario: A requirement added by hand leaves the list
    Given I have added the framework "Security policy" with a PDF of pages:
      | page | line                                 |
      | 1    | 5.1 Access control                   |
      | 2    | The supplier shall encrypt backups.  |
    When I add the requirement "5.2" with the title "Encryption" and the description "The supplier shall encrypt backups."
    Then the extraction check of "Security policy" does not show "The supplier shall encrypt backups."

  @US-070 @iso27001-A.8.32 @integration
  Scenario: The numbers are logged when the version is activated
    Given I have added the framework "Security policy" with a PDF of pages:
      | page | line                     |
      | 1    | 5.1 Access control       |
      | 2    | Background of the policy |
    When I activate version 1 of "Security policy"
    Then the "CatalogActivated" entry contains "1 requirement, 1 page without requirements"
