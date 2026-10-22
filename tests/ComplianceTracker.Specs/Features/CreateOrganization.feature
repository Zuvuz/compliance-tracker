Feature: Create organization

  @US-001 @iso27001-A.5.2
  Scenario: Create an organization and become its administrator
    Given I am "Anna"
    When I create the organization "Acme" with CVR "12345678", sector "Manufacturing" and size "Small"
    Then the organization "Acme" exists
    And "Anna" is a user in "Acme" with the role "Admin"

  @US-001
  Scenario: Name is required
    Given I am "Anna"
    When I create an organization with no name and CVR "12345678"
    Then the creation is rejected with the reason "Name is required"

  @US-001
  Scenario Outline: CVR must be exactly 8 digits
    Given I am "Anna"
    When I create the organization "Acme" with CVR "<cvr>", sector "Manufacturing" and size "Small"
    Then the creation is rejected with the reason "CVR must be 8 digits"

    Examples:
      | cvr       |
      | 1234567   |
      | 123456789 |
      | 1234ABCD  |

  @US-001
  Scenario: CVR must be unique
    Given an organization "Acme" exists with CVR "12345678"
    And I am "Bo"
    When I create the organization "Beta" with CVR "12345678", sector "Health" and size "Medium"
    Then the creation is rejected with the reason "CVR already in use"

  @US-001
  Scenario: Sector is required
    Given I am "Anna"
    When I create the organization "Acme" with CVR "12345678", no sector and size "Small"
    Then the creation is rejected with the reason "Sector is required"

  @US-001 @iso27001-A.8.15
  Scenario: Creating an organization is logged
    Given I am "Anna"
    When I create the organization "Acme" with CVR "12345678", sector "Manufacturing" and size "Small"
    Then an activity log entry exists with user "Anna", action "OrganizationCreated" and organization "Acme"