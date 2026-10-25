Feature: Upload frameworks as PDF

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-005 @iso27001-7.5.3 @integration
  Scenario: Add a framework with a PDF
    When I add the framework "ISO/IEC 27001" with the file "iso27001.pdf"
    Then the framework list shows "ISO/IEC 27001" with version 1

  @US-005 @integration
  Scenario: Add several frameworks
    When I add the framework "ISO/IEC 27001" with the file "iso27001.pdf"
    And I add the framework "Security policy" with the file "policy.pdf"
    Then the framework list shows "ISO/IEC 27001" and "Security policy"

  @US-005 @iso27001-7.5.3 @integration
  Scenario: A new upload becomes a new version
    Given the framework "ISO/IEC 27001" has been uploaded
    When I upload a new version of "ISO/IEC 27001" with the file "iso27001-v2.pdf"
    Then the framework list shows "ISO/IEC 27001" with version 2
    And version 1 of "ISO/IEC 27001" can still be opened

  @US-005 @integration
  Scenario: The same framework name cannot be added twice
    Given the framework "ISO/IEC 27001" has been uploaded
    When I add the framework "iso/iec 27001" with the file "iso27001.pdf"
    Then I see the error "Framework already exists. Upload a new version instead"

  @US-005 @asvs-v5.2.1 @asvs-v5.2.2 @integration
  Scenario Outline: Only PDF files up to 20 MB are accepted
    When I add the framework "Security policy" with <file>
    Then I see the error "<error>"

    Examples:
      | file                                    | error                              |
      | a text file named "policy.pdf"          | Only PDF files are allowed         |
      | a PDF of 21 MB named "policy.pdf"       | The file must be 20 MB or smaller  |

  @US-005 @asvs-v5.3.2 @asvs-v5.4.1 @integration
  Scenario: The user's file name is never trusted
    When I add the framework "Security policy" with the file "../../policy.pdf"
    Then the framework list shows the file name "policy.pdf"
    And opening the PDF returns the file name "policy.pdf"

  @US-005 @iso27001-A.5.15 @integration
  Scenario: Other organisations' files are hidden
    Given "Bo" has created the organisation "Beta ApS" with the framework "Beta policy"
    When I open the PDF of "Beta policy"
    Then the page is not found

  @US-005 @iso27001-A.8.15 @integration
  Scenario: Uploads are logged
    When I add the framework "ISO/IEC 27001" with the file "iso27001.pdf"
    Then the activity log for "Acme ApS" has "CatalogImported" for "Anna" concerning "ISO/IEC 27001" version 1