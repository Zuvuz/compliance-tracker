// Integrationstestene deler én PostgreSQL-database og tømmer den før hvert scenarie.
// Kører to features samtidig, sletter de hinandens data, så features køres én ad gangen.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
