using System.Xml.Linq;

namespace AthleteDay.UnitTests;

/// <summary>Every server and staff-app text exists in English and German (AC-54).</summary>
public class ResourceParityTests
{
    private static readonly string Root = FindRepoRoot();

    private static IEnumerable<string> EnglishResourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root, "src"), "*.resx", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileNameWithoutExtension(f).Contains('.'))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(file => Path.GetRelativePath(Root, file));

    public static TheoryData<string> EnglishResources() => new(EnglishResourceFiles());

    [Fact]
    [Trait("AC", "AC-54")]
    public void Server_and_staff_texts_live_in_resource_files()
    {
        var projects = EnglishResourceFiles().Select(r => r.Split(Path.DirectorySeparatorChar)[1]).Distinct();

        Assert.Superset(
            new HashSet<string> { "AthleteDay.Api", "AthleteDay.Contracts", "AthleteDay.Core", "AthleteDay.Staff" },
            projects.ToHashSet());
    }

    [Theory]
    [MemberData(nameof(EnglishResources))]
    [Trait("AC", "AC-54")]
    public void German_resources_have_the_same_texts_as_English(string englishFile)
    {
        var germanFile = Path.ChangeExtension(englishFile, ".de.resx");
        Assert.True(File.Exists(Path.Combine(Root, germanFile)), $"{germanFile} is missing.");

        var english = Entries(englishFile);
        var german = Entries(germanFile);

        Assert.Equal(english.Keys.Order(), german.Keys.Order());
        Assert.All(german, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value), $"{germanFile}: {entry.Key} is empty."));
    }

    private static Dictionary<string, string> Entries(string relativePath) =>
        XDocument.Load(Path.Combine(Root, relativePath)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AthleteDay.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (AthleteDay.slnx) not found.");
    }
}
