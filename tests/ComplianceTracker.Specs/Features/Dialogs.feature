Feature: Dialogs for the forms that interrupt a page

  # US-166: forms that interrupt a page open in the shared dialog (a title, the form's own primary button and Cancel).
  # The form stays in the page and is moved into the dialog by site.js, so without JavaScript it is a normal form (R5).
  # These scenarios check what the pages give the script, and that the forms still work as normal forms.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"
    And the project "Acme Portal" has the user story "[US-004] Sign in"

  @US-166 @integration
  Scenario: The requirement page's forms open in the shared dialog
    When I open the requirement "I.2(d)" from the overview
    Then the page has the shared dialog for screen readers
    And "Add evidence" opens the dialog "Add evidence to I.2(d)"
    And "Link a story" opens the dialog "Link a user story to I.2(d)"
    And the "Not applicable" form opens the dialog "Mark I.2(d) as not applicable"

  @US-166 @integration
  Scenario: Without JavaScript the form in a dialog still works
    When I add the evidence "Access policy" with the link "https://example.com/policy" to "I.2(d)"
    Then the row "I.2(d)" shows the evidence "Access policy"
