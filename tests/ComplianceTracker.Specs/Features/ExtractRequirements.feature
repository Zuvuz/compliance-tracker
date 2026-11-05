Feature: Extract and activate requirements

  # US-007: an uploaded framework PDF becomes a draft with the requirements found in its text.
  # The member checks and corrects the draft and then activates it, which locks it.
  # Projects can only use active versions.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-007 @iso27001-7.5.3 @integration
  Scenario: Requirements are suggested from the PDF
    When I add the framework "ISO/IEC 27001" with a PDF containing:
      | line                                          |
      | Annex A                                       |
      | A.5.15 Access control                         |
      | Rules to control access shall be established. |
      | A.5.16 Identity management                    |
    Then the draft of "ISO/IEC 27001" version 1 lists "A.5.15" and "A.5.16"
    And the requirement "A.5.15" has the title "Access control"

  @US-007 @integration
  Scenario: A table of contents does not create extra rows
    When I add the framework "ISO/IEC 27001" with a PDF containing:
      | line                                          |
      | A.5.15 Access control .................. 12   |
      | A.5.16 Identity management ............. 13   |
      | A.5.15 Access control                         |
      | Rules to control access shall be established. |
      | A.5.16 Identity management                    |
    Then the draft of "ISO/IEC 27001" version 1 lists "A.5.15" and "A.5.16"
    And the requirement "A.5.15" has the title "Access control"

  @US-007 @integration
  Scenario: The draft can be corrected
    Given the framework "ISO/IEC 27001" has a draft with "A.5.15 Access contr"
    When I change the title of "A.5.15" to "Access control"
    Then the draft lists "A.5.15" with the title "Access control"

  @US-007 @integration
  Scenario: A wrong row can be deleted and a missing one added by hand
    Given the framework "ISO/IEC 27001" has a draft with "A.5.15 Access control"
    When I delete the requirement "A.5.15"
    And I add the requirement "A.5.16" with the title "Identity management"
    Then the draft lists only "A.5.16"

  @US-007 @integration
  Scenario: A code is used once per version
    Given the framework "ISO/IEC 27001" has a draft with "A.5.15 Access control"
    When I add the requirement "A.5.15" with the title "Twice"
    Then I see the error "Requirement A.5.15 is already in the list"

  @US-007 @iso27001-7.5.3 @integration
  Scenario: An activated version is locked
    Given the framework "ISO/IEC 27001" has a draft with "A.5.15 Access control"
    When I activate version 1 of "ISO/IEC 27001"
    And I try to change the title of "A.5.15"
    Then I see the error "An active version cannot be changed"
    And the page of version 1 of "ISO/IEC 27001" offers no changes

  @US-007 @iso27001-7.5.3 @integration
  Scenario: A new activated version becomes current
    Given version 1 of "ISO/IEC 27001" is active
    And version 2 of "ISO/IEC 27001" is a draft
    When I activate version 2 of "ISO/IEC 27001"
    Then version 2 is the current version of "ISO/IEC 27001"
    And version 1 of "ISO/IEC 27001" is still active

  @US-007 @integration
  Scenario: No requirements found
    When I add the framework "Security policy" with a PDF without text
    Then I see "No requirements found. Add them by hand below."

  @US-007 @integration
  Scenario: A version without requirements cannot be activated
    When I add the framework "Security policy" with a PDF without text
    And I activate version 1 of "Security policy"
    Then I see the error "Add at least one requirement before activating"

  @US-007 @iso27001-7.5.3 @integration
  Scenario: Projects can only use active versions
    Given the framework "ISO/IEC 27001" has a draft with "A.5.15 Access control"
    And the project "Acme Portal" exists
    Then "ISO/IEC 27001" cannot be chosen for a project
    When I activate version 1 of "ISO/IEC 27001"
    And I add "ISO/IEC 27001" to the project "Acme Portal"
    Then the project "Acme Portal" uses "ISO/IEC 27001" version 1

  @US-007 @iso27001-A.5.15 @integration
  Scenario: Other organisations cannot see the requirements
    Given the framework "ISO/IEC 27001" has a draft with "A.5.15 Access control"
    And "Bo" has created the organisation "Beta ApS"
    When "Bo" opens version 1 of "ISO/IEC 27001"
    Then the page is not found

  @US-007 @iso27001-A.8.15 @integration
  Scenario: Activation is logged
    Given the framework "ISO/IEC 27001" has a draft with "A.5.15 Access control"
    When I activate version 1 of "ISO/IEC 27001"
    Then the activity log for "Acme ApS" has "CatalogActivated" for "Anna" concerning "ISO/IEC 27001" version 1
