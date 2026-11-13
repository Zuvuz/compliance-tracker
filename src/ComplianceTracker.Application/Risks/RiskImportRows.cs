using System.Globalization;
using ComplianceTracker.Domain.Risks;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Risks
{
    // US-049: one row of an imported risk register, as the preview shows it (R3)
    public record RiskImportRow(int Number, string Title)
    {
        public string? Description { get; init; }
        public int? Likelihood { get; init; }
        public int? Impact { get; init; }
        public RiskTreatment Treatment { get; init; } = RiskTreatment.Reduce;
        public string? Reason { get; init; }
        public DateOnly? LastReviewed { get; init; }

        // R5: the owner as written, the member it names (null: not a member, the importer becomes the owner)
        public string? OwnerText { get; init; }
        public User? Owner { get; init; }

        // R4: a risk with the same title is already in the project
        public bool IsDuplicate { get; init; }

        public IReadOnlyList<string> Errors { get; init; } = [];

        public bool IsValid => Errors.Count == 0;
        public bool OwnerIsMember => OwnerText is null || Owner is not null;

        // R3: the level, when likelihood and impact are valid
        public RiskLevel? Level => Likelihood is { } l && Impact is { } i ? Risk.LevelOf(l * i) : null;
    }

    // US-049 R1–R5, R9: reads the rows of a sheet (the first row names the columns) and checks every row before
    // anything is saved. Pure, so the rules can be tested without a database.
    public static class RiskImportRows
    {
        public const int MaxTitleLength = 200;
        public const int MaxDescriptionLength = 4000;
        public const int MaxReasonLength = 1000;

        // R1: the template's columns, in its order
        public static readonly string[] Columns = ["Title", "Description", "Likelihood", "Impact", "Owner", "Treatment", "Reason", "Last reviewed"];

        // R2: the errors of the file as a whole; empty when its rows can be checked
        public static IReadOnlyList<string> FileErrors(IReadOnlyList<IReadOnlyList<string>> sheet)
        {
            var header = sheet.Count == 0 ? [] : sheet[0].Select(Key).ToList();
            if (!header.Contains("title") || !header.Contains("likelihood") || !header.Contains("impact"))
            {
                return ["The file needs the columns Title, Likelihood and Impact in its first row"];
            }

            var rows = sheet.Skip(1).Count(r => !IsBlank(r));
            return rows switch
            {
                0 => ["The file has no risks"],
                > RiskImport.MaxRows => [$"The file has {rows} rows; at most {RiskImport.MaxRows} can be imported"],
                _ => []
            };
        }

        // R2–R5: every row with its errors. Rows are numbered from 1 after the header; blank rows are skipped.
        public static IReadOnlyList<RiskImportRow> Read(
            IReadOnlyList<IReadOnlyList<string>> sheet, IEnumerable<User> members, IEnumerable<string> existingTitles, DateOnly today)
        {
            if (FileErrors(sheet).Count > 0)
            {
                return [];
            }

            var header = sheet[0].Select(Key).ToList();
            var active = members.Where(m => m.IsActive).ToList();
            var existing = existingTitles.Select(t => t.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<RiskImportRow>();

            var number = 0;
            foreach (var cells in sheet.Skip(1))
            {
                if (IsBlank(cells))
                {
                    continue;
                }

                number++;
                string? Cell(string key)
                {
                    var at = header.IndexOf(key);
                    return at < 0 || at >= cells.Count || string.IsNullOrWhiteSpace(cells[at]) ? null : cells[at].Trim();
                }

                var errors = new List<string>();
                void Error(string text) => errors.Add($"Row {number}: {text}");

                var title = Cell("title") ?? "";
                if (title.Length == 0)
                {
                    Error("Title is required");
                }
                else if (title.Length > MaxTitleLength)
                {
                    Error($"Title must be {MaxTitleLength} characters or fewer");
                }
                else if (seen.TryGetValue(title, out var first))
                {
                    Error($"the title is also in row {first}");
                }
                else
                {
                    seen[title] = number;
                }

                var description = Cell("description");
                if (description?.Length > MaxDescriptionLength)
                {
                    Error($"Description must be {MaxDescriptionLength} characters or fewer");
                }

                var likelihood = Scale(Cell("likelihood"), "Likelihood", Error);
                var impact = Scale(Cell("impact"), "Impact", Error);

                var treatment = RiskTreatment.Reduce;
                if (Cell("treatment") is { } treatmentText && !Enum.TryParse(treatmentText, ignoreCase: true, out treatment)
                    || !Enum.IsDefined(treatment))
                {
                    Error("Treatment must be Reduce, Accept, Avoid or Transfer");
                    treatment = RiskTreatment.Reduce;
                }

                var reason = Cell("reason");
                if (treatment == RiskTreatment.Accept && reason is null)
                {
                    Error("Accept needs a reason"); // R6, as US-042 R4
                }
                else if (reason?.Length > MaxReasonLength)
                {
                    Error($"Reason must be {MaxReasonLength} characters or fewer");
                }

                DateOnly? reviewed = null;
                if (Cell("lastreviewed") is { } dateText)
                {
                    if (Date(dateText) is not { } date)
                    {
                        Error("Last reviewed must be a date, e.g. 2026-05-01");
                    }
                    else if (date > today)
                    {
                        Error("Last reviewed cannot be in the future");
                    }
                    else
                    {
                        reviewed = date;
                    }
                }

                var ownerText = Cell("owner");
                var owner = ownerText is null
                    ? null
                    : active.FirstOrDefault(m => string.Equals(m.Name, ownerText, StringComparison.OrdinalIgnoreCase)
                                                 || string.Equals(m.Email, ownerText, StringComparison.OrdinalIgnoreCase));

                rows.Add(new RiskImportRow(number, title)
                {
                    Description = description,
                    Likelihood = likelihood,
                    Impact = impact,
                    Treatment = treatment,
                    Reason = reason,
                    LastReviewed = reviewed,
                    OwnerText = ownerText,
                    Owner = owner,
                    IsDuplicate = title.Length > 0 && existing.Contains(title),
                    Errors = errors
                });
            }

            return rows;
        }

        // "Last reviewed" → "lastreviewed"
        private static string Key(string column)
            => new string(column.Trim().TrimStart('﻿').ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        private static bool IsBlank(IReadOnlyList<string> cells) => cells.All(string.IsNullOrWhiteSpace);

        private static int? Scale(string? text, string name, Action<string> error)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && value is >= Risk.MinScale and <= Risk.MaxScale)
            {
                return value;
            }

            error($"{name} must be {Risk.MinScale} to {Risk.MaxScale}");
            return null;
        }

        // ISO dates, Danish dates, and Excel's day numbers (a date cell in .xlsx is a number)
        private static DateOnly? Date(string text)
        {
            string[] formats = ["yyyy-MM-dd", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy", "dd/MM/yyyy", "d/M/yyyy"];
            if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }

            if (DateTime.TryParseExact(text, ["yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp))
            {
                return DateOnly.FromDateTime(stamp);
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 20000 and <= 80000
                ? DateOnly.FromDateTime(DateTime.FromOADate(Math.Floor(serial)))
                : null;
        }
    }
}
