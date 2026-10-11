using System.Net;
using System.Net.Http.Json;
using AthleteDay.Contracts;
using AthleteDay.Staff.Resources;
using Microsoft.Extensions.Localization;

namespace AthleteDay.Staff.Services;

public sealed record ApiResult<T>(T? Value, IReadOnlyDictionary<string, string[]>? FieldErrors = null, string? Error = null)
{
    public bool Succeeded => FieldErrors is null && Error is null;
}

public sealed class StaffApi(HttpClient http, PersonaState persona, IStringLocalizer<Strings> text)
{
    private sealed class ProblemResponse
    {
        public string? Title { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }

    public async Task<ApiResult<List<AthleteDto>>> GetAthletesAsync() =>
        await SendAsync<List<AthleteDto>>(() => http.GetAsync("api/athletes"));

    public Task<ApiResult<TrainingSessionDto>> LogSessionAsync(LogSessionRequest request) =>
        SendAsync<TrainingSessionDto>(() => http.PostAsJsonAsync("api/sessions", request));

    public Task<ApiResult<WellnessSummaryDto>> SubmitWellnessAsync(WellnessCheckRequest request) =>
        SendAsync<WellnessSummaryDto>(() => http.PutAsJsonAsync("api/wellness", request));

    public Task<ApiResult<NutritionTodayDto>> SetNutritionAsync(NutritionTargetRequest request) =>
        SendAsync<NutritionTodayDto>(() => http.PutAsJsonAsync("api/nutrition-targets", request));

    public Task<ApiResult<PhysioNoteDto>> AddPhysioNoteAsync(PhysioNoteRequest request) =>
        SendAsync<PhysioNoteDto>(() => http.PostAsJsonAsync("api/physio-notes", request));

    private async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException)
        {
            return new(default, Error: text["ApiUnreachable", http.BaseAddress!]);
        }

        if (response.IsSuccessStatusCode)
            return new(await response.Content.ReadFromJsonAsync<T>());

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest when await response.Content.ReadFromJsonAsync<ProblemResponse>() is { Errors: { } errors } =>
                new(default, FieldErrors: errors),
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new(default, Error: text["NotAllowed", text[$"Role_{persona.Current.Role}"]]),
            _ => new(default, Error: text["ApiAnswered", (int)response.StatusCode, response.ReasonPhrase ?? ""])
        };
    }
}
