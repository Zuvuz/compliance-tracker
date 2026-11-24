using ComplianceTracker.Domain.Isms;
using ComplianceTracker.Domain.Users;

namespace ComplianceTracker.Application.Isms
{
    // US-214: one row of an imported asset list, checked
    public sealed record AssetImportRow(int Number, string Name, AssetKind? Kind, User? Owner, string? Classification, bool HoldsPersonalData,
        string? Description, IReadOnlyList<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    // US-214: reads an asset list from a spreadsheet (the first row names the columns) and checks every row before anything
    // is saved; one bad row stops the whole import, like the risk import (US-049). Pure, so the rules are tested on their own.
    public static class AssetImportRows
    {
        public const int MaxRows = 500;

        public static readonly string[] Columns = ["Name", "Kind", "Owner", "Classification", "Personal data", "Description"];

        public static IReadOnlyList<string> FileErrors(IReadOnlyList<IReadOnlyList<string>> sheet)
        {
            var header = sheet.Count == 0 ? [] : sheet[0].Select(Key).ToList();
            var needed = new[] { "name", "kind", "owner", "classification" };
            if (needed.Any(n => !header.Contains(n)))
            {
                var has = sheet.Count == 0 ? "" : string.Join(", ", sheet[0].Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim().TrimStart('﻿')));
                return ["The file needs the columns Name, Kind, Owner and Classification in its first row" + (has.Length > 0 ? $". It has: {has}" : "")];
            }

            var rows = sheet.Skip(1).Count(r => !IsBlank(r));
            return rows switch
            {
                0 => ["The file has no assets"],
                > MaxRows => [$"The file has {rows} rows; at most {MaxRows} can be imported"],
                _ => []
            };
        }

        // Every row with its errors. Rows are numbered from 1 after the header; blank rows are skipped. The owner is a
        // member's name or e-mail; R6: a name already in the organisation or twice in the file is an error.
        public static IReadOnlyList<AssetImportRow> Read(IReadOnlyList<IReadOnlyList<string>> sheet, IEnumerable<User> members,
            ClassificationScheme scheme, IEnumerable<string> existingNames)
        {
            if (FileErrors(sheet).Count > 0)
            {
                return [];
            }

            var header = sheet[0].Select(Key).ToList();
            var active = members.Where(m => m.IsActive).ToList();
            var existing = existingNames.Select(n => n.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<AssetImportRow>();
            var number = 0;
            foreach (var cells in sheet.Skip(1))
            {
                if (IsBlank(cells))
                {
                    continue;
                }

                number++;
                string Cell(string column)
                {
                    var index = header.IndexOf(column);
                    return index >= 0 && index < cells.Count ? cells[index].Trim() : "";
                }

                var errors = new List<string>();
                var name = Cell("name");
                if (name.Length == 0)
                {
                    errors.Add("The name is missing");
                }
                else if (name.Length > Asset.MaxNameLength)
                {
                    errors.Add($"The name must be {Asset.MaxNameLength} characters or fewer");
                }
                else if (existing.Contains(name))
                {
                    errors.Add($"{name} is already in the inventory");
                }
                else if (!seen.Add(name))
                {
                    errors.Add($"{name} is in the file twice");
                }

                AssetKind? kind = Enum.TryParse<AssetKind>(Cell("kind"), true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(Cell("kind"), out _)
                    ? parsed
                    : null;
                if (kind is null)
                {
                    errors.Add($"The kind must be one of {string.Join(", ", Enum.GetNames<AssetKind>())}");
                }

                var ownerText = Cell("owner");
                var owner = active.FirstOrDefault(m => string.Equals(m.Name, ownerText, StringComparison.OrdinalIgnoreCase)
                                                       || string.Equals(m.Email, ownerText, StringComparison.OrdinalIgnoreCase));
                if (owner is null)
                {
                    errors.Add(ownerText.Length == 0 ? "The owner is missing" : $"{ownerText} is not a member of the organisation");
                }

                var classification = scheme.Find(Cell("classification"));
                if (classification is null)
                {
                    errors.Add(Cell("classification").Length == 0
                        ? "The classification is missing"
                        : $"{Cell("classification")} is not a level in the classification scheme");
                }

                var personal = Cell("personal data").ToLowerInvariant() is "yes" or "y" or "true" or "1" or "ja" or "x";
                var description = Cell("description");
                if (description.Length > Asset.MaxDescriptionLength)
                {
                    errors.Add($"The description must be {Asset.MaxDescriptionLength} characters or fewer");
                }

                rows.Add(new AssetImportRow(number, name, kind, owner, classification, personal, description.Length == 0 ? null : description, errors));
            }

            return rows;
        }

        private static string Key(string column) => column.Trim().TrimStart('﻿').ToLowerInvariant();

        private static bool IsBlank(IReadOnlyList<string> cells) => cells.All(string.IsNullOrWhiteSpace);
    }
}
