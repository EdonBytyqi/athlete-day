using System.Net;
using System.Net.Http.Json;
using AthleteDay.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace AthleteDay.IntegrationTests;

/// <summary>Server texts follow the request's Accept-Language header (AC-57).</summary>
[Collection(ApiCollection.Name)]
public sealed class LanguageTests(ApiFactory api)
{
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich")).DateTime);

    private HttpClient ClientAs(string role, string? language, string? user = null)
    {
        var client = api.ClientAs(role, user);
        if (language is not null) client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(language);
        return client;
    }

    [Theory]
    [InlineData("de-CH", "Die Dauer muss zwischen 5 und 300 Minuten liegen.")]
    [InlineData("de", "Die Dauer muss zwischen 5 und 300 Minuten liegen.")]
    [InlineData("en", "Duration must be between 5 and 300 minutes.")]
    [InlineData("fr", "Duration must be between 5 and 300 minutes.")]
    [InlineData(null, "Duration must be between 5 and 300 minutes.")]
    [Trait("AC", "AC-57")]
    public async Task Validation_messages_follow_the_request_language(string? language, string expected)
    {
        var response = await ClientAs(Roles.Coach, language).PostAsJsonAsync("/api/sessions",
            new LogSessionRequest { AthleteId = 1, DurationMinutes = 2, Rpe = 5 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expected, (await response.ReadAsync<ValidationProblemDetails>()).Errors["DurationMinutes"]);
    }

    [Fact]
    [Trait("AC", "AC-57")]
    public async Task Api_errors_follow_the_request_language()
    {
        var response = await ClientAs(Roles.Coach, "de").PostAsJsonAsync("/api/sessions",
            new LogSessionRequest { AthleteId = 999_999, Date = Today, DurationMinutes = 60, Rpe = 5 });

        var problem = await response.ReadAsync<ValidationProblemDetails>();
        Assert.Equal(["Unbekannte Athletin oder unbekannter Athlet."], problem.Errors["AthleteId"]);
    }

    [Fact]
    [Trait("AC", "AC-57")]
    public async Task Readiness_reasons_follow_the_request_language()
    {
        var athlete = await api.CreateAthleteAsync();
        await api.ClientAs(Roles.Athlete, athlete.Code).PutAsJsonAsync("/api/wellness",
            new WellnessCheckRequest { AthleteId = athlete.Id, Date = Today, SleepHours = 5.5m, Soreness = 4, Mood = 4 });

        var row = (await ClientAs(Roles.Coach, "de-CH").GetFromJsonAsync<List<DashboardRowDto>>("/api/dashboard"))!
            .Single(r => r.AthleteId == athlete.Id);

        Assert.Contains("Schlaf unter 6 h (5.5 h)", row.Readiness.Reasons);
        Assert.Contains("Starker Muskelkater (4/5)", row.Readiness.Reasons);
    }

    [Fact]
    [Trait("AC", "AC-57")]
    public async Task Import_row_errors_are_written_in_the_language_of_the_upload()
    {
        var coach = ClientAs(Roles.Coach, "de");
        var csv = $"""
            athlete_code,date,test_type,value,unit
            ATH-NOBODY,{Today.AddDays(-1):yyyy-MM-dd},CMJ,38.4,cm
            """;

        var accepted = await coach.PostCsvAsync("jump.csv", csv);
        var jobId = (await accepted.ReadAsync<ImportJobDto>()).Id;
        ImportJobDto job;
        var attempts = 0;
        do
        {
            await Task.Delay(100);
            job = (await coach.GetFromJsonAsync<ImportJobDto>($"/api/imports/{jobId}"))!;
        } while (job.Status is ImportStatus.Queued or ImportStatus.Processing && ++attempts < 50);

        Assert.Equal("Unbekannter Athletencode 'ATH-NOBODY'.", Assert.Single(job.Errors).Reason);
    }
}
