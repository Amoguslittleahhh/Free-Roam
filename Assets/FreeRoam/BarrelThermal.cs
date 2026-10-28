using System;

// Lumped-capacity model of a rifle barrel's hottest section (a few inches ahead of the chamber, where the M4 test measured the maximum).
//   C dT/dt = -(h_conv + h_rad) * A * (T - T_air)      each round adds Q joules:  T += Q / C
// h_conv: Churchill-Bernstein (cross-flow over a cylinder) mixed with Churchill-Chu (natural convection, horizontal cylinder), air properties at the film temperature.
// h_rad : grey-body radiation, emissivity Eps.
// Calibrated against the U.S. Army M4 carbine temperature test (280 rounds at 140-160 rpm: handguard liner ~700 F / 371 C, 10-12 minute cool-down).
// The SAR-21's barrel mass and geometry were not found in the sources used, so M4-class values are ASSUMED for Mass, Diameter, Length, Shroud and Eps.
public class BarrelThermal
{
    public const double Mass = 0.55, SteelC = 490.0, Diameter = 0.022, Length = 0.40, Shroud = 0.9, Eps = 0.8;   // kg, J/(kg K), m, m, fraction of the area open to air, -
    public const double RoundJ = 380.0;                                                                              // J into the hot section per round (calibrated, see above)
    public double T = 30.0;                                                                                          // hot-section temperature, deg C

    public void Shot() { T += RoundJ / (Mass * SteelC); }

    /// <param name="airC">air temperature, C</param><param name="windMs">wind speed, m/s</param><param name="moveMs">the shooter's own speed through the air, m/s</param>
    public void Step(double dt, double airC, double windMs, double moveMs)
    {
        double v = Math.Sqrt(windMs * windMs + moveMs * moveMs);   // relative air speed (the directions are not tracked: a simplification)
        double A = Math.PI * Diameter * Length * Shroud, Cap = Mass * SteelC;
        int n = Math.Max(1, (int)Math.Ceiling(dt / 0.25)); double h = dt / n;
        for (int i = 0; i < n; i++) T -= Loss(T, airC, v) * A * (T - airC) / Cap * h;
    }

    public static double Loss(double Ts, double Ta, double v)   // total heat-transfer coefficient, W/(m2 K)
    {
        double dT = Math.Abs(Ts - Ta); if (dT < 1e-6) return 0.0;
        double Tf = 0.5 * (Ts + Ta) + 273.15, k = 0.0263 * Math.Pow(Tf / 300.0, 0.86), nu = 1.57e-5 * Math.Pow(Tf / 300.0, 1.68), Pr = 0.71, D = Diameter;
        double Re = v * D / nu, NuF = 0.0;
        if (Re * Pr > 0.2) NuF = 0.3 + 0.62 * Math.Sqrt(Re) * Math.Pow(Pr, 1.0 / 3.0) / Math.Pow(1.0 + Math.Pow(0.4 / Pr, 2.0 / 3.0), 0.25) * Math.Pow(1.0 + Math.Pow(Re / 282000.0, 5.0 / 8.0), 0.8);
        double Ra = 9.81 / Tf * dT * D * D * D / (nu * nu / Pr);
        double NuN = Math.Pow(0.60 + 0.387 * Math.Pow(Ra, 1.0 / 6.0) / Math.Pow(1.0 + Math.Pow(0.559 / Pr, 9.0 / 16.0), 8.0 / 27.0), 2.0);
        double Nu = Math.Pow(NuF * NuF * NuF + NuN * NuN * NuN, 1.0 / 3.0), hc = Nu * k / D;
        double Tsk = Ts + 273.15, Tak = Ta + 273.15, hr = Eps * 5.670374e-8 * (Tsk * Tsk + Tak * Tak) * (Tsk + Tak);
        return hc + hr;
    }

    // ---- ice pack on the barrel ----
    // Real values: latent heat of fusion of ice 334 kJ/kg, specific heat of ice 2.09 kJ/(kg K) (a freezer-cold pack starts at -18 C, a common freezer setting).
    // ASSUMED (no source found): pack mass 0.15 kg, a wet-contact conductance of 1000 W/(m2 K) against the steel, and a contact area equal to the bore-side wrap of the 74 mm of
    // barrel between the handguard and the muzzle device (0.0035 m2). The pack stops working once all of its ice is melted.
    public const double IceLatent = 334000.0, IceC = 2090.0, PackMass = 0.15, PackStartC = -18.0, ContactH = 1000.0, ContactArea = 0.0035;
    public bool PackOn; public double PackE;   // PackE: heat absorbed by the pack so far, J
    public static double PackWarm { get { return PackMass * IceC * -PackStartC; } }              // J to bring the pack from its start temperature to 0 C
    public static double PackCap { get { return PackWarm + PackMass * IceLatent; } }            // J to melt it all
    public double PackTempC { get { return PackE < PackWarm ? PackStartC + PackE / (PackMass * IceC) : 0.0; } }
    public double PackLeft { get { return Math.Max(0.0, 1.0 - PackE / PackCap); } }
    public bool PackSpent { get { return PackE >= PackCap; } }
    public int PackStock = 3;   // spare packs carried (the pack in use is not counted); a partly used pack stays in the pouch and is used again before a fresh one is taken
    public bool PackApply()
    {
        if (PackE <= 0.0 || PackSpent) { if (PackStock <= 0) return false; PackStock--; PackE = 0.0; }
        PackOn = true; return true;
    }
    public void PackRemove() { PackOn = false; }
    public void PackStep(double dt)
    {
        if (!PackOn || PackSpent) return;
        double Cap = Mass * SteelC, G = ContactH * ContactArea;
        int n = Math.Max(1, (int)Math.Ceiling(dt / 0.05)); double h = dt / n;
        for (int i = 0; i < n && !PackSpent; i++)
        {
            double Tp = PackTempC, dT = T - Tp; if (dT <= 0.0) break;
            double q = Math.Min(G * dT * h, dT * Cap * 0.5);    // heat out of the barrel into the pack this step
            T -= q / Cap; PackE += q;
        }
    }

    // ---- what the heat does to the shot ----
    // Measured (RECOIL magazine AR-15 test, 16-inch government/M4 profile): a 30-round dump from cold opened the 5-shot group from 1.33 to 2.55 MOA (+1.22 MOA) and moved the
    // point of impact 2 in down and 1 in left (the test distance is not stated; 100 yd assumed). Both effects are scaled in proportion to the temperature rise of the
    // hot section (what this model gives after those 30 rounds, GrowthPerK / DriftPerK below), and carried on linearly: beyond the measured range this is an EXTRAPOLATION.
    public const double GrowthMoaPerK = 1.22 / 42.0, DriftDownMoaPerK = 1.91 / 42.0, DriftLeftMoaPerK = 0.95 / 42.0, EsToSigma = 1.0 / 2.4;   // 42 K = the temperature rise this model gives after 30 rounds at 600 rpm from cold
    public static double Rise(double T, double airC) { return Math.Max(0.0, T - airC); }
}
