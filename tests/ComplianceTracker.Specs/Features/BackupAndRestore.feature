Feature: Backup and restore

  # US-014: the database is backed up every day and the backups are kept for 30 days.
  # The scenarios run the same scripts as the backup service (docker/backup-db.sh and docker/restore-db.sh)
  # inside the test database's PostgreSQL container.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"
    And the framework "Security policy" has the active requirements "SP-1 Passwords" and "SP-2 Backups"

  @US-014 @iso27001-A.8.13 @integration
  Scenario: A backup can be restored
    When the database is backed up
    And the backup is restored into an empty database
    Then the restored database has "Acme ApS", the framework PDF and the log entries

  @US-014 @iso27001-A.8.13 @integration
  Scenario: Backups are kept for 30 days
    Given there are backups from 29 and 30 days ago
    When the database is backed up
    Then there are backups from today and from 29 days ago
    And the backup from 30 days ago is deleted

  @US-014 @iso27001-A.8.13 @integration
  Scenario: A file that is not a backup is refused
    When a file that is not a backup is restored into an empty database
    Then the restore fails
    And the empty database is still empty
