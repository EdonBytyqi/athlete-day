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

    public static AuthorizationBuilder AddAthleteDayPolicies(this AuthorizationBuilder builder) => builder
        .AddPolicy(ReadAthletes, p => p.RequireRole(Roles.Coach, Roles.Physio, Roles.Nutrition, Roles.Athlete))
        .AddPolicy(LogSessions, p => p.RequireRole(Roles.Coach))
        .AddPolicy(ViewDashboard, p => p.RequireRole(Roles.Coach, Roles.Physio, Roles.Nutrition))
        .AddPolicy(SubmitWellness, p => p.RequireRole(Roles.Athlete))
        .AddPolicy(SetNutrition, p => p.RequireRole(Roles.Nutrition))
        .AddPolicy(ViewKitchen, p => p.RequireRole(Roles.Kitchen, Roles.Nutrition))
        .AddPolicy(HealthNotes, p => p.RequireRole(Roles.Physio))
        .AddPolicy(ImportDeviceData, p => p.RequireRole(Roles.Coach, Roles.Physio))
        .AddPolicy(ViewAudit, p => p.RequireRole(Roles.Admin))
        .AddPolicy(ManageSquad, p => p.RequireRole(Roles.Coach));
}
