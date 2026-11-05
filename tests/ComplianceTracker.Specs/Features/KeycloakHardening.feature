Feature: Keycloak stops password guessing and requires a one-time password

  # US-089: the realm file (keycloak/compliance-tracker-realm.json) turns on brute force protection and a password
  # policy, and every user must set up a one-time password (TOTP). Decided: MFA for everyone, not only Admins,
  # because Keycloak does not know the tracker's roles. These scenarios use a real Keycloak with the same realm file.

  Background:
    Given an empty database

  @US-089 @iso27001-A.8.5 @asvs-V2.2 @integration @keycloak
  Scenario: The Keycloak realm has brute force protection and a password policy
    Given Keycloak is started with the tracker's realm
    Then Keycloak's brute force protection is on
    And Keycloak's password policy requires at least 12 characters

  @US-089 @iso27001-A.8.5 @asvs-V2.8 @integration @keycloak
  Scenario: A new Keycloak user must set up a one-time password
    Given Keycloak has a user "bodil" with e-mail "bodil@acme.dk"
    Then Keycloak requires "bodil" to set up a one-time password

  @US-089 @iso27001-A.8.5 @asvs-V2.8 @integration @keycloak
  Scenario: Signing in through Keycloak sets up the one-time password and then asks for it
    Given Keycloak has a user "cara" with e-mail "cara@acme.dk"
    When I sign in through Keycloak as "cara"
    Then Keycloak asked "cara" to set up a one-time password
    And I am signed in to the application as "cara"
    When I sign in through Keycloak as "cara" again
    Then Keycloak asked "cara" for the one-time code
    And I am signed in to the application as "cara"

  @US-089 @iso27001-A.8.5 @asvs-V2.2 @integration @keycloak
  Scenario: Too many wrong passwords lock the Keycloak account
    Given Keycloak has a user "dan" with e-mail "dan@acme.dk"
    When someone tries 5 wrong passwords for "dan" in Keycloak
    Then Keycloak has locked "dan" for now
