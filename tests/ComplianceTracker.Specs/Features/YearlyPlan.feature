Feature: The ISMS yearly plan

  # US-215: the recurring tasks that keep the ISMS running, each with an interval, an owner and a deadline (ISO/IEC 27001
  # 8.1). A done task gets its next deadline from the old one, and its evidence is linked to its controls as an organisation
  # document, so the projects get it (US-136). The starting plan is suggested from the controls in use in the SoA. The page
  # is "Yearly plan" in the ISMS area.

  Background:
    Given an empty database
    And the time is "2027-01-05T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Webshop" exists
    And the framework "ISO/IEC 27001:2022" has the active requirements "A.5.18 Access rights" and "A.8.13 Information backup"
    And "Webshop" uses "ISO/IEC 27001:2022"

  @US-215 @iso27001-8.1 @integration
  Scenario: A done task gets its next deadline
    Given the quarterly task "Access review" owned by "Anna" with the deadline "2026-12-31" for the control "A.5.18"
    When I mark "Access review" as done on "2027-01-05" with the evidence "access-review-q4.pdf"
    Then the next deadline of "Access review" is "2027-03-31"
    And the evidence "access-review-q4.pdf" is linked to the control "A.5.18"

  @US-215 @iso27001-8.1 @integration
  Scenario: The starting plan is suggested from the SoA
    When I open the yearly plan
    Then the task "Backup restore test" is suggested
    And the plan has no tasks

  @US-215 @iso27001-8.1 @integration
  Scenario: An accepted suggestion joins the plan
    When I accept the suggested task "Backup restore test" with the first deadline "2027-03-31"
    Then the plan has the task "Backup restore test" due "2027-03-31"
    And the task "Backup restore test" is no longer suggested

  @US-215 @iso27001-8.1 @integration
  Scenario: A task that needs evidence cannot be done without it
    Given I accept the suggested task "Backup restore test" with the first deadline "2027-03-31"
    When I mark "Backup restore test" as done on "2027-01-05" without evidence
    Then the yearly plan says "Backup restore test needs evidence: restore log"

  @US-215 @iso27001-8.1 @iso27001-10.2 @integration
  Scenario: An overdue task becomes a nonconformity
    Given the quarterly task "Access review" owned by "Anna" with the deadline "2026-12-31" for the control "A.5.18"
    When I raise a nonconformity for the overdue task "Access review"
    Then the nonconformity "Access review not done by 31-12-2026" is "Open" from "MissedDeadline"

  @US-215 @iso27001-8.1 @integration
  Scenario: A member who does not decide compliance reads the plan
    Given the quarterly task "Access review" owned by "Anna" with the deadline "2026-12-31" for the control "A.5.18"
    And "Dan" has joined "Acme ApS" as "Developer"
    And I am signed in as "Dan"
    When I open the yearly plan
    Then the yearly plan is read-only
