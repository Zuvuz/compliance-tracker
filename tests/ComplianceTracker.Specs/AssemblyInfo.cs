// The integration tests share one PostgreSQL database and empty it before each scenario.
// If two features ran at the same time, they would delete each other's data, so features run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
