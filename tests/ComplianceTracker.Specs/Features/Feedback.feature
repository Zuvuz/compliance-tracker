Feature: Send feedback as a user story

  # US-063: "Send feedback" in the footer opens a form shaped like a user story, with an optional screenshot.
  # Only the system admin sees the feedback, and can make a GitHub issue of it without personal data, or dismiss it.

  Background:
    Given an empty database
    And "Anna" has created the organisation "Acme ApS"
    And I am signed in as "Anna"

  @US-063 @integration
  Scenario: The footer links to the feedback form
    When I open the front page
    Then the footer has the link "Send feedback"

  @US-063 @integration
  Scenario: Send feedback with a screenshot
    When I send the feedback "As a" "Developer" "I want" "to filter the risks by owner" "so that" "I see my own risks" with the screenshot "risks.png"
    Then I see "Thank you, your feedback has been sent"
    And the feedback is stored with the screenshot and its SHA-256
    And the activity log for "Acme ApS" has "FeedbackSent" for "Anna"

  @US-063 @iso27001-A.5.34 @integration
  Scenario: The system admin creates a GitHub issue without personal data
    Given "Anna" has sent the feedback "to filter the risks by owner" with a screenshot
    And I am signed in as the system admin "Rune"
    When I create a GitHub issue from the feedback "to filter the risks by owner"
    Then GitHub has an issue with the labels "user-story", "status:draft" and "feedback"
    And the issue does not mention "Anna" or "Acme ApS"
    And the issue says "A screenshot is attached in the tracker"
    And the feedback shows the issue's number

  @US-063 @iso27001-A.5.15 @integration
  Scenario: Members cannot see other people's feedback
    Given "Anna" has sent the feedback "to filter the risks by owner" with a screenshot
    And "Bo" has joined "Acme ApS" as "Developer"
    When "Bo" opens the address "/System/Feedback"
    Then access is denied

  @US-063 @iso27001-A.8.12 @integration
  Scenario: A screenshot that is not an image is refused
    When I send feedback with the screenshot "notes.png" that is really text
    Then I see the error "The screenshot must be a PNG or JPG image"

  @US-063 @integration
  Scenario: GitHub cannot be reached
    Given "Anna" has sent the feedback "to filter the risks by owner" with a screenshot
    And GitHub cannot be reached
    And I am signed in as the system admin "Rune"
    When I create a GitHub issue from the feedback "to filter the risks by owner"
    Then I see the error "GitHub could not be reached; the feedback is kept"
    And the feedback is still "New"

  @US-063 @integration
  Scenario: Dismissing needs a reason and keeps the feedback
    Given "Anna" has sent the feedback "to filter the risks by owner" with a screenshot
    And I am signed in as the system admin "Rune"
    When I dismiss the feedback "to filter the risks by owner" with the reason "Already planned in US-049"
    Then the feedback is still "Dismissed"

  @US-063 @integration
  Scenario: At most 10 feedbacks an hour
    Given "Anna" has sent 10 feedbacks in the last hour
    When I send the feedback "As a" "Developer" "I want" "one more thing" "so that" "it is better" with the screenshot "risks.png"
    Then I see the error "You have sent a lot of feedback; try again later"
