using UnityEngine;

// The air the player stands in. Cooling of the barrel (BarrelThermal) reads it. Nothing else in the project sets it yet: a weather or day/night system can write
// AirTempC and WindMs directly. F6 cycles the test presets.
public static class Atmosphere
{
    public static float AirTempC = 32f, WindMs = 1f;   // default: a Singapore afternoon
    public static string Name = "Singapore afternoon";
    public const float WindToDeg = 90f;   // the wind blows towards this compass heading (0 = world +z, 90 = world +x): a steady wind from the west, a crosswind when you face north
    public const float PressurePa = 101325f;   // sea level (Singapore); weather pressure changes are not modelled
    /// <summary>Wind as a world velocity (m/s).</summary>
    public static Vector3 WindVec { get { float a = WindToDeg * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * WindMs; } }
    /// <summary>Dry-air density from the ideal gas law, rho = p / (R_specific T), R_specific = 287.058 J/(kg K).</summary>
    public static float AirDensity { get { return PressurePa / (287.058f * (AirTempC + 273.15f)); } }
    /// <summary>Speed of sound in dry air, c = sqrt(gamma R T), gamma = 1.4.</summary>
    public static float SpeedOfSound { get { return Mathf.Sqrt(1.4f * 287.058f * (AirTempC + 273.15f)); } }
    static readonly (string n, float t, float w)[] Presets =
    {
        ("Singapore afternoon", 32f, 1f), ("Breezy, 3 m/s", 32f, 3f), ("Storm gusts, 9 m/s", 26f, 9f), ("Cold night", 5f, 1f), ("Desert noon", 45f, 3f), ("Arctic wind", -20f, 10f),
    };
    static int idx;
    public static void Cycle() { idx = (idx + 1) % Presets.Length; Name = Presets[idx].n; AirTempC = Presets[idx].t; WindMs = Presets[idx].w; }
}
