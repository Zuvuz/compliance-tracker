Feature: Factories for extension points

  # US-083 (B36): where there are several implementations and the choice is made while the app runs, a factory
  # picks one by key (keyed services). The factory's interface lives where the choice is made; IServiceProvider
  # is only used in Infrastructure. Behaviour does not change: every other feature is unchanged and green.

  @US-083 @iso27001-A.8.27
  Scenario: Application and Domain do not use the service provider
    When the Application and Domain assemblies are read
    Then no class depends on IServiceProvider

  @US-083 @iso27001-A.8.27 @integration
  Scenario: Every file type and kind of document has exactly one reader
    When the readers are listed from the factory
    Then each pair of file type and kind has exactly one reader
    And each reader says it reads its own file type and kind

  @US-083 @iso27001-A.8.32 @integration
  Scenario: An unknown AI provider stops the app with a clear message
    Given the setting "Ai:Provider" is "Banana"
    When the app with that setting starts
    Then it stops with "Ai:Provider must be one of: Ollama, OpenAICompatible"
