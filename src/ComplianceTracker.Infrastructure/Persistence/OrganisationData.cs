using System.Data.Common;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using ComplianceTracker.Application.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

namespace ComplianceTracker.Infrastructure.Persistence
{
    // US-184: every table that belongs to one organisation, found from the EF model, so a new table is never forgotten.
    // A table belongs to it when it has the organisation's id (OrganizationId or OwnerOrganizationId), a project of it
    // (ProjectId), or a foreign key to a row that belongs to it.
    public class OrganisationData : IOrganisationData
    {
        // Kept when the organisation is closed: the activity log is append-only (US-091) and points at the organisation and
        // its members, which are anonymised instead of deleted
        private static readonly HashSet<string> Kept = new(StringComparer.Ordinal) { "ActivityLog", "Organizations", "Users" };

        private readonly ComplianceTrackerDbContext _db;

        public OrganisationData(ComplianceTrackerDbContext db)
        {
            _db = db;
        }

        private sealed record Table(string Name, string Key, string Where, IReadOnlyList<(string Column, bool Binary)> Columns, IReadOnlyList<string> Principals);

        // The tables and the WHERE that selects the organisation's rows (with @org), dependents after their principals
        private IReadOnlyList<Table> Tables()
        {
            var types = _db.Model.GetEntityTypes()
                .Where(t => !t.IsOwned() && t.GetTableName() is not null && t.FindPrimaryKey() is not null)
                .GroupBy(t => t.GetTableName()!)
                .Select(g => g.OrderBy(t => t.BaseType is null ? 0 : 1).First())
                .ToDictionary(t => t.GetTableName()!);
            var where = new Dictionary<string, string>();

            string? Find(IEntityType type, int depth)
            {
                var table = type.GetTableName()!;
                if (where.TryGetValue(table, out var known))
                {
                    return known;
                }

                string? Column(string property) => type.FindProperty(property)?.GetColumnName();
                string? clause = table == "Organizations" ? $"\"{Column("Id")}\" = @org"
                    : Column("OrganizationId") is { } org ? $"\"{org}\" = @org"
                    : Column("OwnerOrganizationId") is { } owner ? $"\"{owner}\" = @org"
                    : Column("ProjectId") is { } project ? $"\"{project}\" IN (SELECT \"Id\" FROM \"Projects\" WHERE \"OrganizationId\" = @org)"
                    : null;
                if (clause is null && depth < 4)
                {
                    // Who did something (a key to Users) says less about whose a row is than what it belongs to, and may be
                    // empty; it is used only when nothing else leads to the organisation
                    foreach (var key in type.GetForeignKeys().Where(k => k.Properties.Count == 1)
                                 .OrderBy(k => k.PrincipalEntityType.GetTableName() == "Users" ? 1 : 0))
                    {
                        var principal = key.PrincipalEntityType;
                        if (principal.GetTableName() is { } principalTable && principalTable != table && types.ContainsKey(principalTable)
                            && Find(types[principalTable], depth + 1) is { } principalWhere)
                        {
                            clause = $"\"{key.Properties[0].GetColumnName()}\" IN (SELECT \"{key.PrincipalKey.Properties[0].GetColumnName()}\" FROM \"{principalTable}\" WHERE {principalWhere})";
                            break;
                        }
                    }
                }

                if (clause is not null)
                {
                    where[table] = clause;
                }

                return clause;
            }

            var tables = new List<Table>();
            foreach (var (name, type) in types)
            {
                if (Find(type, 0) is not { } clause)
                {
                    continue;
                }

                var columns = type.GetProperties().Select(p => (p.GetColumnName(), p.ClrType == typeof(byte[]))).Distinct().ToList();
                var principals = type.GetForeignKeys().Select(k => k.PrincipalEntityType.GetTableName()!).Where(t => t != name).Distinct().ToList();
                // The key as text for the files' names; a composite key joins its parts with "-"
                var key = $"concat_ws('-', {string.Join(", ", type.FindPrimaryKey()!.Properties.Select(p => $"\"{p.GetColumnName()}\"::text"))})";
                tables.Add(new Table(name, key, clause, columns, principals));
            }

            // Principals first (export order); deleting goes the other way
            var ordered = new List<Table>();
            var placed = new HashSet<string>();
            while (ordered.Count < tables.Count)
            {
                var next = tables.Where(t => !placed.Contains(t.Name) && t.Principals.All(p => placed.Contains(p) || tables.All(o => o.Name != p))).ToList();
                if (next.Count == 0)
                {
                    next = tables.Where(t => !placed.Contains(t.Name)).ToList(); // a cycle: keep going in name order
                }

                foreach (var table in next.OrderBy(t => t.Name, StringComparer.Ordinal))
                {
                    ordered.Add(table);
                    placed.Add(table.Name);
                }
            }

            return ordered;
        }

        // R2: a JSON file per table with the rows (files left out), each file on its own, and a contents page with SHA-256
        public async Task<byte[]> ExportAsync(Guid organizationId, string organisationName, DateTime at, CancellationToken cancellationToken = default)
        {
            var connection = _db.Database.GetDbConnection();
            var opened = connection.State != System.Data.ConnectionState.Open;
            if (opened)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                using var memory = new MemoryStream();
                var contents = new StringBuilder();
                contents.AppendLine($"All data of {organisationName}").AppendLine($"Exported {at:yyyy-MM-dd'T'HH:mm:ss'Z'}").AppendLine();
                contents.AppendLine("Every table as JSON (data/), every stored file on its own (files/<table>/<id>-<column>), each with its SHA-256.").AppendLine();
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
                {
                    void Add(string path, byte[] content)
                    {
                        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                        entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                        using var stream = entry.Open();
                        stream.Write(content);
                        contents.AppendLine($"  {path}  SHA-256 {Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()}");
                    }

                    foreach (var table in Tables())
                    {
                        var plain = table.Columns.Where(c => !c.Binary).Select(c => $"\"{c.Column}\"").ToList();
                        var json = await ScalarAsync(connection,
                            $"SELECT COALESCE(json_agg(x), '[]'::json)::text FROM (SELECT {string.Join(", ", plain)} FROM \"{table.Name}\" WHERE {table.Where}) x",
                            organizationId, cancellationToken);
                        Add($"data/{table.Name}.json", Encoding.UTF8.GetBytes(json ?? "[]"));

                        foreach (var (column, _) in table.Columns.Where(c => c.Binary))
                        {
                            await using var command = Command(connection,
                                $"SELECT {table.Key}, \"{column}\" FROM \"{table.Name}\" WHERE {table.Where} AND \"{column}\" IS NOT NULL", organizationId);
                            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                            while (await reader.ReadAsync(cancellationToken))
                            {
                                Add($"files/{table.Name}/{reader.GetString(0)}-{column}", (byte[])reader.GetValue(1));
                            }
                        }
                    }

                    var page = zip.CreateEntry("00-contents.txt", CompressionLevel.Optimal);
                    page.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    await using var writer = new StreamWriter(page.Open(), new UTF8Encoding(false));
                    await writer.WriteAsync(contents.ToString());
                }

                return memory.ToArray();
            }
            finally
            {
                if (opened)
                {
                    await connection.CloseAsync();
                }
            }
        }

        // R3: everything of the organisation except the activity log, the organisation's row and its members, which the
        // domain anonymises. Dependents first. Running it again deletes nothing more.
        // One transaction; it names the organisation being closed, so the locked reports' trigger lets them go (and only them)
        public async Task DeleteAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            await _db.Database.ExecuteSqlRawAsync("SELECT set_config('ct.closing_organisation', @org::text, true)",
                [new NpgsqlParameter("org", organizationId)], cancellationToken);
            // Dependents first; a table still referenced by one not deleted yet (the order cannot always be known from the
            // model, e.g. keys between versions and their framework) is tried again in the next round
            var pending = Tables().Where(t => !Kept.Contains(t.Name)).Reverse().ToList();
            PostgresException? last = null;
            for (var round = 0; pending.Count > 0 && round < 10; round++)
            {
                var deleted = new List<Table>();
                foreach (var table in pending)
                {
                    await transaction.CreateSavepointAsync("table", cancellationToken);
                    try
                    {
                        await _db.Database.ExecuteSqlRawAsync($"DELETE FROM \"{table.Name}\" WHERE {table.Where}",
                            [new NpgsqlParameter("org", organizationId)], cancellationToken);
                        await transaction.ReleaseSavepointAsync("table", cancellationToken);
                        deleted.Add(table);
                    }
                    catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                    {
                        await transaction.RollbackToSavepointAsync("table", cancellationToken);
                        last = error;
                    }
                }

                if (deleted.Count == 0)
                {
                    throw last!;
                }

                pending.RemoveAll(deleted.Contains);
            }

            if (pending.Count > 0)
            {
                throw last!;
            }

            await transaction.CommitAsync(cancellationToken);
        }

        // The organisation's rows per table, for checking that nothing is left
        public async Task<IReadOnlyDictionary<string, long>> CountAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            var connection = _db.Database.GetDbConnection();
            var opened = connection.State != System.Data.ConnectionState.Open;
            if (opened)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                var counts = new Dictionary<string, long>();
                foreach (var table in Tables())
                {
                    counts[table.Name] = long.Parse(await ScalarAsync(connection, $"SELECT COUNT(*)::text FROM \"{table.Name}\" WHERE {table.Where}", organizationId, cancellationToken) ?? "0");
                }

                return counts;
            }
            finally
            {
                if (opened)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private DbCommand Command(DbConnection connection, string sql, Guid organizationId)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction();
            command.Parameters.Add(new NpgsqlParameter("org", organizationId));
            return command;
        }

        private async Task<string?> ScalarAsync(DbConnection connection, string sql, Guid organizationId, CancellationToken cancellationToken)
        {
            await using var command = Command(connection, sql, organizationId);
            return (await command.ExecuteScalarAsync(cancellationToken))?.ToString();
        }
    }
}
