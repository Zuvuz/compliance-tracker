using System.Text.Json;
using ComplianceTracker.Infrastructure.Persistence;
using ComplianceTracker.LoadSeed;
using Microsoft.EntityFrameworkCore;

// US-138: seeds an empty test database for the load test.
// dotnet run --project tests/load/seed -- "<connection string>" users.json
// users.json: [{ "Sub": "...", "Name": "...", "Email": "..." }, …], the first is the Admin.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: LoadSeed <connection string> <users.json>");
    return 2;
}

var users = JsonSerializer.Deserialize<List<LoadUser>>(await File.ReadAllTextAsync(args[1])) ?? [];
var options = new DbContextOptionsBuilder<ComplianceTrackerDbContext>().UseNpgsql(args[0]).Options;
await using var db = new ComplianceTrackerDbContext(options);
try
{
    await LoadSeed.RunAsync(db, users, DateTime.UtcNow);
}
catch (InvalidOperationException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}

Console.WriteLine($"Seeded \"{LoadSeed.Organisation}\" with {users.Count} users");
return 0;
