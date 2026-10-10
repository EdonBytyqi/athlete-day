using AthleteDay.Core;

namespace AthleteDay.UnitTests;

public class DeviceCsvValidatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 10);
    private static readonly HashSet<string> Athletes = ["ATH-001", "ATH-002"];
    private const string Header = "athlete_code,date,test_type,value,unit";

    private static DeviceCsvResult Validate(params string[] rows) =>
        DeviceCsvValidator.Validate(string.Join("\n", [Header, .. rows]), Athletes, Today);

    [Fact]
    [Trait("AC", "AC-24")]
    public void Valid_rows_are_accepted_and_normalised()
    {
        var result = Validate("ath-001,2026-10-09,cmj,38.4,CM", "ATH-002,2026-10-09,SPRINT_30M,4.12,s");

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.ValidRows.Count);
        Assert.Equal(new DeviceMeasurementRow(2, "ATH-001", new DateOnly(2026, 10, 9), "CMJ", 38.4m, "cm"), result.ValidRows[0]);
    }

    [Theory]
    [InlineData("ATH-099,2026-10-09,CMJ,38,cm", "athlete_code", "Unknown athlete code")]
    [InlineData("ATH-001,09.10.2026,CMJ,38,cm", "date", "yyyy-MM-dd")]
    [InlineData("ATH-001,2026-10-11,CMJ,38,cm", "date", "future")]
    [InlineData("ATH-001,2026-10-09,BENCH,90,kg", "test_type", "Unknown test type")]
    [InlineData("ATH-001,2026-10-09,CMJ,abc,cm", "value", "not a number")]
    [InlineData("ATH-001,2026-10-09,CMJ,\"38,4\",cm", "value", "decimal comma")]
    [InlineData("ATH-001,2026-10-09,CMJ,140,cm", "value", "plausible range")]
    [InlineData("ATH-001,2026-10-09,CMJ,38,mm", "unit", "expected 'cm'")]
    [InlineData("ATH-001,2026-10-09,CMJ,38", "row", "Expected 5 columns")]
    [Trait("AC", "AC-24")]
    public void Invalid_rows_are_rejected_with_column_and_reason(string row, string column, string reasonPart)
    {
        var result = Validate(row);

        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.RowNumber);
        Assert.Equal(column, error.Column);
        Assert.Contains(reasonPart, error.Reason);
        Assert.Equal(row, error.RawLine);
        Assert.Empty(result.ValidRows);
    }

    [Fact]
    [Trait("AC", "AC-23")]
    public void Good_rows_survive_next_to_bad_ones()
    {
        var result = Validate("ATH-001,2026-10-09,CMJ,38,cm", "ATH-099,2026-10-09,CMJ,38,cm", "ATH-002,2026-10-09,CMJ,41,cm");

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.ValidRows.Count);
        Assert.Equal(3, Assert.Single(result.Errors).RowNumber);
    }

    [Fact]
    [Trait("AC", "AC-24")]
    public void Duplicate_rows_are_rejected()
    {
        var result = Validate("ATH-001,2026-10-09,CMJ,38,cm", "ATH-001,2026-10-09,CMJ,39,cm");

        Assert.Single(result.ValidRows);
        Assert.Contains("Duplicate", Assert.Single(result.Errors).Reason);
    }

    [Fact]
    [Trait("AC", "AC-24")]
    public void Semicolon_exports_from_excel_are_accepted()
    {
        var csv = "athlete_code;date;test_type;value;unit\nATH-001;2026-10-09;CMJ;38.4;cm";

        var result = DeviceCsvValidator.Validate(csv, Athletes, Today);

        Assert.Single(result.ValidRows);
    }

    [Fact]
    [Trait("AC", "AC-25")]
    public void Wrong_header_is_a_file_level_error()
    {
        var result = DeviceCsvValidator.Validate("code,day,value\nATH-001,2026-10-09,38", Athletes, Today);

        Assert.True(result.HasFileError);
        Assert.Empty(result.ValidRows);
    }

    [Fact]
    [Trait("AC", "AC-25")]
    public void Empty_file_is_a_file_level_error()
    {
        Assert.True(DeviceCsvValidator.Validate("", Athletes, Today).HasFileError);
    }

    [Fact]
    [Trait("AC", "AC-25")]
    public void Blank_lines_and_windows_line_endings_are_ignored()
    {
        var result = DeviceCsvValidator.Validate($"{Header}\r\nATH-001,2026-10-09,CMJ,38,cm\r\n\r\n", Athletes, Today);

        Assert.Equal(1, result.TotalRows);
        Assert.Single(result.ValidRows);
    }

    [Fact]
    [Trait("AC", "AC-24")]
    public void Quoted_cells_keep_their_delimiter()
    {
        Assert.Equal(["a", "b,c", "d\"e"], DeviceCsvValidator.SplitLine("a,\"b,c\",\"d\"\"e\"", ','));
    }
}
