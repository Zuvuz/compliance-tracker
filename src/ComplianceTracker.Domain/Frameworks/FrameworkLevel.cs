namespace ComplianceTracker.Domain.Frameworks
{
    // US-142 R2: who follows a framework. Product: a project, e.g. the CRA or the AI Act. Organisation: the whole
    // company, e.g. NIS2; followed in the organisation area, never by a project.
    public enum FrameworkLevel
    {
        Product,
        Organisation
    }
}
