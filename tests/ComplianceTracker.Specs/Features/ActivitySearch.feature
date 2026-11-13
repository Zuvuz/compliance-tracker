Feature: Search, filter and sort a project's activity log

  # US-101: the Activity tab can be searched, filtered by period, action group and user, sorted and paged.
  # The filters stand in the address, so a filtered view can be shared with an auditor.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists

  @US-101 @iso27001-A.8.15 @integration
  Scenario: Searching the details
    Given the activity log of "Acme Portal" has an entry with the details "I.2(d): MFA config"
    And the activity log of "Acme Portal" has an entry with the details "I.2(e): Encryption at rest"
    When I search the activity of "Acme Portal" for "mfa"
    Then I see 1 entry
    And it has the details "I.2(d): MFA config"

  @US-101 @iso27001-A.8.15 @integration
  Scenario: Filtering by period and action group
    Given "Acme Portal" has evidence entries on "31-10-2026", "02-11-2026" and "04-11-2026"
    When I filter the activity of "Acme Portal" from "01-11-2026" to "04-11-2026" in the group "Evidence"
    Then I see 2 entries

  @US-101 @integration
  Scenario: Filtering on what the system did by itself
    Given the system has written an entry about "Acme Portal"
    When I filter the activity of "Acme Portal" on the user "System"
    Then I see 1 entry

  @US-101 @integration
  Scenario: Sorting oldest first
    Given the activity log of "Acme Portal" has an entry with the details "Later"
    When I sort the activity of "Acme Portal" by "Oldest first"
    Then the first entry is "ProjectCreated"

  @US-101 @integration
  Scenario: Paging
    Given the activity log of "Acme Portal" has 126 entries
    When I open the activity of "Acme Portal"
    Then I see "1–50 of 126 entries"
    And there is a link to the next page

  @US-101 @integration
  Scenario: A filtered view can be shared by its address
    When I open the activity of "Acme Portal" with "?q=MFA&group=evidence"
    Then the search field contains "MFA"
    And the group "Evidence" is chosen

  @US-101 @integration
  Scenario: A value in the address that cannot be read is ignored
    When I open the activity of "Acme Portal" with "?from=yesterday"
    Then I see "The date 'yesterday' was not understood and is ignored"

  @US-101 @integration
  Scenario: No entries match
    When I search the activity of "Acme Portal" for "nothing like this"
    Then I see "No entries match the filters"

  @US-101 @iso27001-A.5.15 @integration
  Scenario: Another organisation cannot search the log
    Given "Bo" has created the organisation "Beta ApS"
    When "Bo" searches the activity of "Acme Portal" for "MFA"
    Then the page is not found

  @US-101
  Scenario: Every action belongs to a group
    Then every action of the activity log belongs to an action group

  @US-101 @iso27001-A.8.28 @integration
  Scenario: Wildcards in the search are searched for as text
    Given the activity log of "Acme Portal" has an entry with the details "100% covered"
    And the activity log of "Acme Portal" has an entry with the details "Half covered"
    When I search the activity of "Acme Portal" for "%"
    Then I see 1 entry
    And it has the details "100% covered"

  # #52: groups such as sign-ins can be left out; the log itself never changes
  @US-101 @iso27001-A.8.15 @integration
  Scenario: Groups can be hidden
    Given the activity log of "Acme Portal" has an entry with the details "I.2(d): MFA config"
    When I open the activity of "Acme Portal" with "?hide=evidence"
    Then I see 1 entry
    And the first entry is "ProjectCreated"
    And the group "Evidence" is ticked to be hidden
    When I open the activity of "Acme Portal" with "?hide=evidence,nonsense"
    Then the page says the filter "The action group 'nonsense' was not understood and is ignored"
