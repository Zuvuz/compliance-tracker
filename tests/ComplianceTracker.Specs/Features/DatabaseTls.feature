Feature: Encrypted connection to the database

  # US-084: the whole stack's database (postgres-full) only accepts connections over TLS, and the web app,
  # the migrations and the backup verify its certificate. These scenarios start PostgreSQL with the same
  # entrypoint script and pg_hba file as docker-compose.yml, with a certificate from a test CA.
  # The Docker CI job checks the real stack the same way.

  Background:
    Given a PostgreSQL that only accepts TLS with a certificate from a test CA

  @US-084 @iso27001-A.8.24 @asvs-V9.2.2 @integration
  Scenario: The app connects with TLS and verifies the certificate
    When the app connects with the test CA
    Then the connection is encrypted

  @US-084 @iso27001-A.8.20 @integration
  Scenario: A connection without TLS is refused
    When a client connects without TLS
    Then the connection is refused

  @US-084 @asvs-V9.2.2 @integration
  Scenario: A certificate from another CA is not trusted
    When the app connects with another CA
    Then the connection is refused
