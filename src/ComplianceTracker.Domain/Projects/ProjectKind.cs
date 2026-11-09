namespace ComplianceTracker.Domain.Projects
{
    // US-142: a project is a product, or the organisation area (one per organisation)
    public enum ProjectKind
    {
        Product,
        Organisation
    }
}
