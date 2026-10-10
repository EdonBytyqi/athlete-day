using System.Text.RegularExpressions;

namespace AthleteDay.UnitTests;

/// <summary>
/// Keeps docs/spec/requirements.md and the tests in sync: every acceptance criterion needs a test,
/// and every test tag must point to a criterion that exists.
/// </summary>
[Trait("Category", "Spec")]
public partial class SpecTraceabilityTests
{
    [GeneratedRegex(@"^\|\s*(AC-\d{2})\s*\|", RegexOptions.Multiline)]
    private static partial Regex SpecCriterion();

    [GeneratedRegex(@"Trait\(\s*""AC""\s*,\s*""(AC-\d{2})""\s*\)")]
    private static partial Regex CSharpTag();

    [GeneratedRegex(@"\[(AC-\d{2})\]")]
    private static partial Regex AngularTag();

    private static readonly string Root = FindRepoRoot();

    [Fact]
    public void Every_acceptance_criterion_is_verified_by_at_least_one_test()
    {
        var untested = SpecifiedCriteria().Except(TaggedCriteria()).Order().ToList();

        Assert.True(untested.Count == 0,
            $"These acceptance criteria in docs/spec/requirements.md have no test: {string.Join(", ", untested)}");
    }

    [Fact]
    public void Tests_only_reference_criteria_that_exist_in_the_spec()
    {
        var unknown = TaggedCriteria().Except(SpecifiedCriteria()).Order().ToList();

        Assert.True(unknown.Count == 0,
            $"These test tags are not in docs/spec/requirements.md: {string.Join(", ", unknown)}");
    }

    [Fact]
    public void Acceptance_criteria_ids_are_unique()
    {
        var duplicates = SpecCriterion().Matches(Spec()).GroupBy(m => m.Groups[1].Value)
            .Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }

    private static string Spec() => File.ReadAllText(Path.Combine(Root, "docs", "spec", "requirements.md"));

    private static HashSet<string> SpecifiedCriteria() =>
        SpecCriterion().Matches(Spec()).Select(m => m.Groups[1].Value).ToHashSet();

    private static HashSet<string> TaggedCriteria()
    {
        var csharp = SourceFiles(Path.Combine(Root, "tests"), "*.cs")
            .SelectMany(f => CSharpTag().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value));
        var angular = SourceFiles(Path.Combine(Root, "web", "dashboard", "src"), "*.spec.ts")
            .SelectMany(f => AngularTag().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value));
        return csharp.Concat(angular).ToHashSet();
    }

    private static IEnumerable<string> SourceFiles(string dir, string pattern) =>
        Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains("node_modules"));

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AthleteDay.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (AthleteDay.slnx) not found.");
    }
}
