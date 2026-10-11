using AthleteDay.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace AthleteDay.Api.Auth;

/// <summary>
/// Need-to-know access per discipline. The kitchen gets portions and carbohydrates, never health data;
/// only the physio reads free-text health notes.
/// </summary>
public static class Policies
{
    public const string ReadAthletes = nameof(ReadAthletes);
    public const string LogSessions = nameof(LogSessions);
    public const string ViewDashboard = nameof(ViewDashboard);
    public const string SubmitWellness = nameof(SubmitWellness);
    public const string SetNutrition = nameof(SetNutrition);
    public const string ViewKitchen = nameof(ViewKitchen);
    public const string HealthNotes = nameof(HealthNotes);
    public const string ImportDeviceData = nameof(ImportDeviceData);
    public const string ViewAudit = nameof(ViewAudit);
    public const string ManageSquad = nameof(ManageSquad);
    public const string WellnessHistory = nameof(WellnessHistory);
    public const string ReadSports = nameof(ReadSports);
    public const string ManageSports = nameof(ManageSports);
    public const string CoachingNotes = nameof(CoachingNotes);
    public const string WriteCoaching = nameof(WriteCoaching);
    public const string ReadPlan = nameof(ReadPlan);
    public const string Documents = nameof(Documents);

    public static AuthorizationBuilder AddAthleteDayPolicies(this AuthorizationBuilder builder) => builder
        // The admin needs names and codes to file identity documents (AC-92).
        .AddPolicy(ReadAthletes, p => p.RequireRole(Roles.Coach, Roles.Physio, Roles.Nutrition, Roles.Athlete, Roles.Admin))
        .AddPolicy(LogSessions, p => p.RequireRole(Roles.Coach))
        .AddPolicy(ViewDashboard, p => p.RequireRole(Roles.Coach, Roles.Physio, Roles.Nutrition))
        .AddPolicy(SubmitWellness, p => p.RequireRole(Roles.Athlete))
        .AddPolicy(SetNutrition, p => p.RequireRole(Roles.Nutrition))
        .AddPolicy(ViewKitchen, p => p.RequireRole(Roles.Kitchen, Roles.Nutrition))
        .AddPolicy(HealthNotes, p => p.RequireRole(Roles.Physio))
        .AddPolicy(ImportDeviceData, p => p.RequireRole(Roles.Coach, Roles.Physio))
        .AddPolicy(ViewAudit, p => p.RequireRole(Roles.Admin))
        .AddPolicy(ManageSquad, p => p.RequireRole(Roles.Coach))
        .AddPolicy(WellnessHistory, p => p.RequireRole(Roles.Coach, Roles.Physio))
        .AddPolicy(ReadSports, p => p.RequireRole(Roles.Coach, Roles.Physio, Roles.Nutrition, Roles.Athlete, Roles.Admin))
        .AddPolicy(ManageSports, p => p.RequireRole(Roles.Admin))
        // Session notes and progress reviews (ADR-12): the coach writes, coach and physio read.
        .AddPolicy(CoachingNotes, p => p.RequireRole(Roles.Coach, Roles.Physio))
        .AddPolicy(WriteCoaching, p => p.RequireRole(Roles.Coach))
        // Training plans: the athlete reads only their own (checked in the endpoint, like the morning check).
        .AddPolicy(ReadPlan, p => p.RequireRole(Roles.Coach, Roles.Physio, Roles.Nutrition, Roles.Athlete))
        // Documents (ADR-15): only roles that see at least one document type; the type is checked per document.
        .AddPolicy(Documents, p => p.RequireRole(Roles.Coach, Roles.Physio, Roles.Admin));
}
