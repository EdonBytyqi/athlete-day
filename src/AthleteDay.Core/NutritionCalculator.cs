namespace AthleteDay.Core;

public static class NutritionCalculator
{
    public static int CarbsGrams(decimal carbsGPerKg, decimal bodyMassKg) =>
        (int)Math.Round(carbsGPerKg * bodyMassKg, MidpointRounding.AwayFromZero);

    /// <summary>A plate size the kitchen can act on without seeing any numbers about the athlete's health.</summary>
    public static string Portion(int energyKcal) => energyKcal switch
    {
        < 2200 => "S",
        < 3000 => "M",
        < 3800 => "L",
        _ => "XL"
    };
}
