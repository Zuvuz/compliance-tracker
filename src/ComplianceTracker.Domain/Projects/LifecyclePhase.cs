namespace ComplianceTracker.Domain.Projects
{
    // Where a product is in its life, from idea to end of support (US-008).
    // Matches the CRA way of thinking: the rules apply to a product for as long as it is supported.
    // Stored as text in the database, so new values never shift existing data.
    public enum LifecyclePhase
    {
        // Idea and planning; nothing built yet
        Planning,

        // Being built
        Development,

        // On the market and in use
        Released,

        // No longer maintained
        EndOfSupport
    }
}
