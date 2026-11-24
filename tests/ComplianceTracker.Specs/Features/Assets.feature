Feature: The inventory of information assets

  # US-214: the organisation keeps an inventory of its information and the systems that hold it, each with an owner and a
  # classification from its own scheme, linked to the projects, the suppliers that host them and the risks that concern them
  # (ISO/IEC 27001 A.5.9–A.5.12). The page is "Assets" in the ISMS area.

  Background:
    Given an empty database
    And the time is "2026-11-24T09:00:00Z"
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-214 @iso27001-a5.9 @integration
  Scenario: An asset with owner and classification
    When I add the asset "Customer database" of the kind "Information" owned by "Anna" as "Confidential"
    Then the inventory shows "Customer database" owned by "Anna" as "Confidential"
    And the activity log has "AssetChanged" saying "Added Customer database (Information, Confidential)"

  @US-214 @iso27001-a5.9 @integration
  Scenario: An asset whose owner left is marked
    Given "Bo" has joined "Acme ApS" as "ComplianceManager"
    And the asset "Customer database" is owned by "Bo"
    When I remove "Bo"
    Then the asset "Customer database" is marked "No owner"

  @US-214 @iso27001-a5.9 @integration
  Scenario: A risk shows the highest classification of its assets, and a supplier shows the assets it hosts
    Given the project "Webshop" exists
    And the risk "Data leak from the webshop" exists in "Webshop"
    And I have added the supplier "CloudHost ApS" delivering "Hosting" with criticality "High" and the rule "NIS2 Art. 21(2)(d)"
    And I add the asset "Logo files" of the kind "Information" owned by "Anna" as "Public"
    When I add the asset "Customer database" of the kind "Information" owned by "Anna" as "Confidential" hosted by "CloudHost ApS" and concerned by the risk "Data leak from the webshop"
    And I link the asset "Logo files" to the risk "Data leak from the webshop"
    Then the risk "Data leak from the webshop" shows the classification "Confidential"
    And the supplier "CloudHost ApS" shows the asset "Customer database"

  @US-214 @gdpr-art30 @integration
  Scenario: The assets with personal data are listed
    Given I add the asset "Customer database" of the kind "Information" owned by "Anna" as "Confidential" with personal data
    And I add the asset "Logo files" of the kind "Information" owned by "Anna" as "Public"
    When I filter the inventory on personal data
    Then the inventory lists only "Customer database"

  @US-214 @iso27001-a5.9 @integration
  Scenario: An asset name is used once
    Given I add the asset "Customer database" of the kind "Information" owned by "Anna" as "Confidential"
    When I add the asset "customer database" of the kind "Software" owned by "Anna" as "Internal"
    Then the assets page says "customer database is already in the inventory"

  @US-214 @iso27001-a5.12 @integration
  Scenario: A classification in use cannot be removed from the scheme
    Given I add the asset "Customer database" of the kind "Information" owned by "Anna" as "Confidential"
    When I set the classification levels to "Public, Internal"
    Then the assets page says "Confidential is used by assets; give them another level first"

  @US-214 @iso27001-a5.9 @integration
  Scenario: Importing a spreadsheet with an unknown classification imports nothing
    When I import the assets file with the rows
      | Name              | Kind        | Owner | Classification |
      | Customer database | Information | Anna  | Confidential   |
      | Payroll           | Information | Anna  | Secret         |
    Then the assets page says "Row 2: Secret is not a level in the classification scheme"
    And the inventory has no assets

  @US-214 @iso27001-a5.9 @integration
  Scenario: Importing a spreadsheet adds the assets
    When I import the assets file with the rows
      | Name              | Kind        | Owner | Classification |
      | Customer database | Information | Anna  | Confidential   |
      | Webshop           | Software    | Anna  | Internal       |
    Then the inventory shows "Webshop" owned by "Anna" as "Internal"

  @US-214 @iso27001-a5.9 @integration
  Scenario: The inventory is exported to Excel
    Given I add the asset "Customer database" of the kind "Information" owned by "Anna" as "Confidential"
    When I download the inventory as Excel
    Then the download is an Excel workbook

  @US-214 @iso27001-a5.9 @integration
  Scenario: A member who does not decide compliance reads the inventory
    Given I add the asset "Customer database" of the kind "Information" owned by "Anna" as "Confidential"
    And "Dan" has joined "Acme ApS" as "Developer"
    And I am signed in as "Dan"
    When I open the assets page
    Then the inventory shows "Customer database" owned by "Anna" as "Confidential"
    And the assets page is read-only
