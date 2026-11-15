Feature: A consultant works for several customers from one login

  # US-187: a consultant is invited into several organisations and switches between them. The chosen organisation is
  # kept in a cookie, but only an organisation the person is a member of can be chosen. A person who already works in an
  # organisation can join another one only with an external role (Consultant or Auditor, B121).

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS" with the project "Acme Portal"
    And "Bo" has created the organisation "Beta A/S" with the project "Beta Shop"

  @US-187 @iso27001-A.5.15 @integration
  Scenario: A consultant switches between customers
    Given "Anna" has invited "Carl" to "Acme ApS" as "Consultant"
    And "Bo" has invited "Carl" to "Beta A/S" as "Consultant"
    When "Carl" switches to "Beta A/S"
    Then "Carl" sees the project "Beta Shop" and not "Acme Portal"
    When "Carl" switches to "Acme ApS"
    Then "Carl" sees the project "Acme Portal" and not "Beta Shop"

  @US-187 @iso27001-A.5.15 @integration
  Scenario: A changed cookie does not open an organisation the person is not a member of
    Given "Anna" has invited "Carl" to "Acme ApS" as "Consultant"
    When "Carl" sets the chosen organisation to "Beta A/S" by hand
    Then "Carl" sees the project "Acme Portal" and not "Beta Shop"
    And "Carl" cannot switch to "Beta A/S"

  @US-187 @iso27001-A.5.18 @integration
  Scenario: A removed consultant no longer sees the customer
    Given "Anna" has invited "Carl" to "Acme ApS" as "Consultant"
    And "Bo" has invited "Carl" to "Beta A/S" as "Consultant"
    And "Carl" has switched to "Acme ApS"
    When "Anna" removes "Carl" from "Acme ApS"
    Then "Carl" sees the project "Beta Shop" and not "Acme Portal"

  @US-187 @integration
  Scenario: A member of an organisation cannot join another one as an employee
    When "Anna" invites "Bo" to "Acme ApS" as "Developer"
    Then the invitation to "Bo" says "You are already a member of an organisation"

  @US-187 @iso27001-A.8.15 @integration
  Scenario: What a consultant does is logged in the customer's log with the role
    Given "Anna" has invited "Carl" to "Acme ApS" as "Consultant"
    When "Carl" creates the project "Acme Shop" in "Acme ApS"
    Then the activity log of "Acme ApS" has an entry by "Carl" as "Consultant"
    And the activity log of "Beta A/S" has no entry by "Carl"

  @US-187 @integration
  Scenario: My customers shows each organisation's status
    Given "Anna" has invited "Carl" to "Acme ApS" as "Consultant"
    And "Bo" has invited "Carl" to "Beta A/S" as "Consultant"
    When "Carl" opens the customer overview
    Then the customer overview lists "Acme ApS" and "Beta A/S"

  @US-187 @integration
  Scenario: A person in one organisation sees no switcher
    When "Anna" opens the projects
    Then there is no organisation switcher
