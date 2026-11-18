Feature: Uploaded test results leave a trace

  # #52 (US-058, US-177): every upload of test results is kept and listed with what it led to, also when it made no
  # suggestions, and says why. The suggestions that wait are listed under the upload with "Accept all". The same file
  # uploaded again says so; withdrawn test results can be uploaded again.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the project "Acme Portal" exists
    And "CRA – Regulation (EU) 2024/2847" is switched on
    And "Acme Portal" uses "CRA – Regulation (EU) 2024/2847"

  @integration
  Scenario: An upload whose tags name a framework the project does not follow says why
    When I upload test results where 3 scenarios tagged "@iso27001-A.8.28" passed
    Then the upload says "0 suggested"
    And the upload says "The tags name frameworks this project does not follow: iso27001"
    And the test results page lists 1 uploads, the newest saying "3 passed"

  @integration
  Scenario: The suggestions that wait are listed under the upload and can be accepted at once
    When I upload test results where 3 scenarios tagged "@cra-I.2(d)" passed
    Then the upload says "1 suggested"
    And the test results page lists "I.2(d)" as waiting
    When I accept all the test results that wait
    Then 1 test run is evidence, from the test results

  @integration
  Scenario: The same file uploaded again says it was suggested before
    Given I have uploaded test results where 3 scenarios tagged "@cra-I.2(d)" passed
    When I upload the same test results again
    Then the upload says "1 already suggested from this file"
    And the upload says "The same file was uploaded before"
    And the test results page lists 2 uploads, the newest saying "0 suggested"

  @integration
  Scenario: Withdrawn test results can be uploaded again
    Given I have uploaded test results where 3 scenarios tagged "@cra-I.2(d)" passed
    And I withdraw the test results because "Wrong branch"
    When I upload the same test results again
    Then the upload says "1 suggested"
    And the test results page lists "I.2(d)" as waiting
