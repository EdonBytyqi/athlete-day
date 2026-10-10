using System.Globalization;
using AthleteDay.Contracts;

namespace AthleteDay.Core;

public sealed record DeviceMeasurementRow(int RowNumber, string AthleteCode, DateOnly Date, string TestType, decimal Value, string Unit);

public sealed record DeviceCsvResult(
    int TotalRows,
    IReadOnlyList<DeviceMeasurementRow> ValidRows,
    IReadOnlyList<ImportRowErrorDto> Errors)
{
    public bool HasFileError => Errors.Any(e => e.RowNumber == 1);
}

/// <summary>
/// Validates a device export (jump tests, GPS) row by row. Good rows are imported, bad rows are reported
/// with the reason, so staff can fix the export instead of losing the whole file.
/// </summary>
public static class DeviceCsvValidator
{
    public const int MaxRows = 10_000;
    public static readonly string[] ExpectedHeader = ["athlete_code", "date", "test_type", "value", "unit"];

    private sealed record TestSpec(string Unit, decimal Min, decimal Max);

    private static readonly Dictionary<string, TestSpec> Tests = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CMJ"] = new("cm", 10, 90),           // counter-movement jump height
        ["SJ"] = new("cm", 10, 90),            // squat jump height
        ["SPRINT_30M"] = new("s", 3.0m, 8.0m),
        ["GPS_DISTANCE"] = new("m", 0, 30_000),
        ["GPS_HSR"] = new("m", 0, 5_000)       // high-speed running distance
    };

    public static IReadOnlyCollection<string> SupportedTests => Tests.Keys;

    public static DeviceCsvResult Validate(string csv, IReadOnlySet<string> knownAthleteCodes, DateOnly today)
    {
        var errors = new List<ImportRowErrorDto>();
        var valid = new List<DeviceMeasurementRow>();

        var lines = csv.Replace("\r\n", "\n").Split('\n');
        var header = lines.FirstOrDefault()?.Trim().TrimStart('\uFEFF') ?? "";
        if (header.Length == 0)
        {
            errors.Add(new(1, "file", "The file is empty.", ""));
            return new(0, valid, errors);
        }

        // Swiss Excel exports often use ';' - accept both delimiters.
        var delimiter = header.Contains(';') ? ';' : ',';
        var columns = header.Split(delimiter).Select(c => c.Trim().ToLowerInvariant()).ToArray();
        if (!columns.SequenceEqual(ExpectedHeader))
        {
            errors.Add(new(1, "header", $"Expected header '{string.Join(',', ExpectedHeader)}'.", header));
            return new(0, valid, errors);
        }

        var seen = new HashSet<(string, DateOnly, string)>();
        var total = 0;

        for (var i = 1; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            if (raw.Length == 0) continue;

            var rowNumber = i + 1;
            total++;
            if (total > MaxRows)
            {
                errors.Add(new(rowNumber, "file", $"More than {MaxRows} rows; the rest of the file was not read.", ""));
                break;
            }

            var reason = ValidateRow(SplitLine(raw, delimiter), knownAthleteCodes, today, out var column, out var row);
            if (reason is not null)
            {
                errors.Add(new(rowNumber, column, reason, raw));
                continue;
            }

            if (!seen.Add((row!.AthleteCode, row.Date, row.TestType)))
            {
                errors.Add(new(rowNumber, "row", "Duplicate of an earlier row (same athlete, date and test).", raw));
                continue;
            }

            valid.Add(row with { RowNumber = rowNumber });
        }

        return new(total, valid, errors);
    }

    /// <summary>Splits one line, honouring double quotes ("4,12" stays one cell).</summary>
    internal static string[] SplitLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { current.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == delimiter && !inQuotes)
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else current.Append(ch);
        }
        cells.Add(current.ToString());
        return cells.ToArray();
    }

    private static string? ValidateRow(string[] cells, IReadOnlySet<string> knownAthleteCodes, DateOnly today,
        out string column, out DeviceMeasurementRow? row)
    {
        row = null;
        column = "row";
        if (cells.Length != ExpectedHeader.Length)
            return $"Expected {ExpectedHeader.Length} columns, found {cells.Length}.";

        var code = cells[0].Trim().ToUpperInvariant();
        var dateText = cells[1].Trim();
        var test = cells[2].Trim().ToUpperInvariant();
        var valueText = cells[3].Trim();
        var unit = cells[4].Trim().ToLowerInvariant();

        column = "athlete_code";
        if (code.Length == 0) return "Athlete code is missing.";
        if (!knownAthleteCodes.Contains(code)) return $"Unknown athlete code '{code}'.";

        column = "date";
        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return $"'{dateText}' is not a date in the format yyyy-MM-dd.";
        if (date > today) return "Date is in the future.";

        column = "test_type";
        if (!Tests.TryGetValue(test, out var spec))
            return $"Unknown test type '{test}'. Supported: {string.Join(", ", Tests.Keys)}.";

        column = "value";
        if (valueText.Contains(',') && !valueText.Contains('.'))
            return $"'{valueText}' uses a decimal comma; use '.' as the decimal separator.";
        if (!decimal.TryParse(valueText, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out var value))
            return $"'{valueText}' is not a number.";
        if (value < spec.Min || value > spec.Max)
            return $"{value.ToString(CultureInfo.InvariantCulture)} {spec.Unit} is outside the plausible range " +
                   $"{spec.Min.ToString(CultureInfo.InvariantCulture)}-{spec.Max.ToString(CultureInfo.InvariantCulture)} {spec.Unit} for {test}.";

        column = "unit";
        if (unit != spec.Unit) return $"Unit '{unit}' does not match {test}; expected '{spec.Unit}'.";

        column = "row";
        row = new DeviceMeasurementRow(0, code, date, test, value, spec.Unit);
        return null;
    }
}
