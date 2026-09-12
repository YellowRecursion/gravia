using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using U = UnitsNet;
using UU = UnitsNet.Units;
using E = EngineeringUnits;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var checks = new List<object>();
int failures = 0;
void Check(string name, Func<double> calculation, double expected, double tolerance = 1e-10)
{
    try {
        double actual = calculation();
        bool pass = double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance * Math.Max(1, Math.Abs(expected));
        checks.Add(new { name, pass, actual, expected });
        if (!pass) failures++;
    } catch (Exception e) { failures++; checks.Add(new { name, pass = false, error = e.GetType().Name + ": " + e.Message }); }
}

// Reference arithmetic and library conversions; no refinery correlation is validated.
Check("U01: 3.6 tonne/h -> kg/s", () => U.MassFlow.FromTonnesPerHour(3.6).KilogramsPerSecond, 1);
Check("U02: 32 F -> K", () => U.Temperature.FromDegreesFahrenheit(32).Kelvins, 273.15);
Check("U02d: delta18 F -> deltaK", () => U.TemperatureDelta.FromDegreesFahrenheit(18).Kelvins, 10);
Check("U05: .82 g/cm3 -> kg/m3", () => U.Density.FromGramsPerCubicCentimeter(.82).KilogramsPerCubicMeter, 820);
Check("U22a: 4 cSt -> m2/s", () => U.KinematicViscosity.FromCentistokes(4).SquareMetersPerSecond, 4e-6);
Check("U22b: 3.2 cP -> Pa.s", () => U.DynamicViscosity.FromCentipoise(3.2).PascalSeconds, .0032);
Check("U25: 500 mg/kg -> percent (mass basis externally asserted)", () => U.Ratio.FromPartsPerMillion(500).Percent, .05);
Check("U dimension mismatch rejected", () => { try { U.UnitConverter.Convert(1, UU.MassFlowUnit.KilogramPerSecond, UU.VolumeFlowUnit.CubicMeterPerSecond); return 0; } catch (ArgumentException) { return 1; } }, 1);
Check("E02: 32 F -> K", () => E.Temperature.FromDegreeFahrenheit(32).Kelvin, 273.15);
Check("E02d: difference50F-32F -> deltaK", () => new E.Temperature(E.Temperature.FromDegreeFahrenheit(50) - E.Temperature.FromDegreeFahrenheit(32)).Kelvin, 10);
Check("E05: .82 g/cm3 -> kg/m3", () => E.Density.FromGramPerCubicCentimeter(.82).KilogramPerCubicMeter, 820);
Check("E25: 500 mg/kg -> percent (mass basis externally asserted)", () => E.Ratio.FromMilligramPerKilogram(500).Percent, .05);
Check("E compound mass/volume -> density", () => new E.Density(E.Mass.FromKilogram(800) / E.Volume.FromCubicMeter(1)).KilogramPerCubicMeter, 800);
Check("E custom mass unit: 2 crates of25kg", () => new E.Mass(2d, new E.Units.MassUnit("audit_crate", 25m)).Kilogram, 50);
Check("E custom compound unit kg/(m3.s)", () => {
    var kg = E.Units.MassUnit.Kilogram.Unit;
    var m3 = E.Units.VolumeUnit.CubicMeter.Unit;
    var s = E.Units.DurationUnit.Second.Unit;
    var source = new E.UnknownUnit(1d, kg / m3 / s);
    var target = E.Units.MassUnit.Gram.Unit / E.Units.VolumeUnit.Liter.Unit / s;
    return E.BaseUnitExtensions.As(source,target);
}, 1);

// Counterexamples demonstrate missing information, NOT implemented Gravia guards.
Check("C03: 2barg + explicit1.01325bar reference", () => 2 + 1.01325, 3.01325);
Check("C04: 10m3/h density800 -> tonne/h", () => 10 * 800 / 1000, 8);
Check("C04: same flow new density700 -> tonne/h", () => 10 * 700 / 1000, 7);
Check("C07: API from SG60/60=.8", () => 141.5 / .8 - 131.5, 45.375);
Check("C07: API is not affine (midpoint discrepancy)", () => {
    Func<double,double> api = sg => 141.5 / sg - 131.5;
    return (api(.8) + api(1)) / 2 - api(.9);
}, 1.96527777777777);
Check("C08: gas standardT273.15 ->293.15, sameP,Z", () => 100 * 293.15 / 273.15, 107.32198425773384);
Check("C10: dryH2 .9, wetH2O .1", () => .9 * (1 - .1), .81);
Check("C12: equal massH2/N2, illustrative molar masses2/28", () => (.5 / 2) / (.5 / 2 + .5 / 28), 14d/15);
Check("C22: 4cSt density800 -> mPa.s", () => 4e-6 * 800 * 1000, 3.2);
Check("C33: 240tonnes over24h, no replenishment", () => 240d / 24, 10);

var observations = new List<object>();
void Observe(string name, Func<object> action)
{
    try { observations.Add(new { name, result = action() }); }
    catch (Exception e) { observations.Add(new { name, error = e.GetType().Name + ": " + e.Message }); }
}
Observe("U Ratio has no mass/mole semantic discriminator", () => U.Ratio.FromPercent(50).DecimalFractions);
#pragma warning disable CS0618 // Deliberate legacy API probe, not a recommended usage.
Observe("E OBSOLETE TemperatureDelta.FromDegreesFahrenheit(18).Kelvins (expected10)", () => E.TemperatureDelta.FromDegreesFahrenheit(18).Kelvins);
#pragma warning restore CS0618
Observe("E difference50F-32F displayed as Celsius (expected delta10)", () => new E.Temperature(E.Temperature.FromDegreeFahrenheit(50) - E.Temperature.FromDegreeFahrenheit(32)).DegreeCelsius);
Observe("E difference50F-32F displayed as Fahrenheit (expected delta18)", () => new E.Temperature(E.Temperature.FromDegreeFahrenheit(50) - E.Temperature.FromDegreeFahrenheit(32)).DegreeFahrenheit);
Observe("E mass/mass -> Ratio", () => new E.Ratio(E.Mass.FromKilogram(1) / E.Mass.FromKilogram(2)).Percent);
Observe("E mol/mol -> same Ratio", () => new E.Ratio(E.AmountOfSubstance.FromMole(1) / E.AmountOfSubstance.FromMole(2)).Percent);
Observe("E parse composite", () => E.UnknownUnit.Eval("1 kg/(m^3*s)").ToString());
Observe("E parse Nm3/h without conditions", () => E.UnknownUnit.Eval("1 Nm3/h").ToString());
Observe("U density condition properties", () => typeof(U.Density).GetProperties().Where(p => p.Name.Contains("Temperature") || p.Name.Contains("Reference") || p.Name.Contains("Phase")).Select(p=>p.Name).ToArray());
Observe("E custom symbol collision: same label different scales", () => new[] {
    new E.Mass(1d, new E.Units.MassUnit("same", 25m)).Kilogram,
    new E.Mass(1d, new E.Units.MassUnit("same", 30m)).Kilogram });

// Coefficient is declared for a KNOWN affine unit; do not infer from f(0),f(1).
var identity = Plan.Prepare(1, 0);
var densityPlan = Plan.Prepare(1000, 0);
Check("Prepared identity excludes conversion", () => identity.HasValue ? 0 : 1, 1);
Check("Prepared density .82 ->820", () => densityPlan!.Value.Apply(.82), 820);
Check("Prepared Celsius20 ->Kelvin", () => Plan.Prepare(1,273.15)!.Value.Apply(20),293.15);
var input = Enumerable.Range(0,100_000).Select(i => .7 + i % 1000 * .0001).ToArray();
var output = new double[input.Length];
var benchmarks = new List<object>();
void Bench(string name, Action operation, double expectedLast)
{
    operation(); // JIT and library warm-up, excluded
    var ms = new double[7]; var bytes = new long[7];
    for (int i=0; i<7; i++) {
        long before = GC.GetAllocatedBytesForCurrentThread();
        long start = Stopwatch.GetTimestamp();
        operation();
        ms[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        bytes[i] = GC.GetAllocatedBytesForCurrentThread() - before;
    }
    Check("Benchmark output: " + name, () => output[^1], expectedLast);
    Check("All outputs: " + name, () => {
        double max = 0;
        double factor = name.StartsWith("copy", StringComparison.Ordinal) ? 1 : 1000;
        for(int i=0;i<input.Length;i++) max=Math.Max(max,Math.Abs(output[i]-input[i]*factor));
        return max;
    },0,1e-9);
    Array.Sort(ms); Array.Sort(bytes);
    benchmarks.Add(new { name, values = input.Length, medianMs = ms[3], minMs = ms[0], maxMs = ms[6], medianAllocatedBytes = bytes[3] });
}
Bench("copy baseline (identity transport)", () => Loops.Copy(input,output), input[^1]);
Bench("prepared affine double loop", () => Loops.Prepared(input,output,densityPlan!.Value), input[^1]*1000);
Bench("UnitsNet typed density per value", () => Loops.UnitsNet(input,output), input[^1]*1000);
Bench("UnitsNet dynamic enum conversion per value", () => Loops.UnitsNetDynamic(input,output), input[^1]*1000);
Bench("EngineeringUnits typed density per value", () => Loops.Engineering(input,output), input[^1]*1000);
var wideIn = Enumerable.Range(0,2_000_000).Select(i => .7 + i %1000 * .0001).ToArray();
var wideOut = new double[wideIn.Length];
Loops.Prepared(wideIn,wideOut,densityPlan!.Value);
var wideTimes = new double[7]; var wideBytes = new long[7];
for (int i=0;i<7;i++) {
    long b=GC.GetAllocatedBytesForCurrentThread(); long t=Stopwatch.GetTimestamp();
    Loops.Prepared(wideIn,wideOut,densityPlan!.Value);
    wideTimes[i]=Stopwatch.GetElapsedTime(t).TotalMilliseconds;
    wideBytes[i]=GC.GetAllocatedBytesForCurrentThread()-b;
}
Array.Sort(wideTimes); Array.Sort(wideBytes);
Check("2m values result", () => wideOut[^1],wideIn[^1]*1000);
benchmarks.Add(new { name="prepared: 100000 candidates x20 fields", values=wideIn.Length, medianMs=wideTimes[3], minMs=wideTimes[0], maxMs=wideTimes[6], medianAllocatedBytes=wideBytes[3] });
var result = new { runtime=Environment.Version.ToString(), framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    unitsNet="5.75.1", engineeringUnits="2.2.143", tieredCompilation=Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
    failures, checks, observations, benchmarks,
    limitations="Isolated conversion; no model, optimizer, IPC, scheduler, concurrency or semantic registry. Known affine density. Warm loops, preallocated arrays. Seven samples in one process, not BenchmarkDotNet." };
string json=JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented=true });
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"../../..","results.json"),json);
Console.WriteLine(json);
return failures==0 ? 0 : 1;

readonly record struct Plan(double Scale,double Offset)
{
    public static Plan? Prepare(double scale,double offset) => scale==1 && offset==0 ? null : new(scale,offset);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Apply(double x) => x*Scale+Offset;
}
static class Loops
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Copy(double[] a,double[] b) { for(int i=0;i<a.Length;i++) b[i]=a[i]; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Prepared(double[] a,double[] b,Plan p) { for(int i=0;i<a.Length;i++) b[i]=p.Apply(a[i]); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UnitsNet(double[] a,double[] b) { for(int i=0;i<a.Length;i++) b[i]=U.Density.FromGramsPerCubicCentimeter(a[i]).KilogramsPerCubicMeter; }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UnitsNetDynamic(double[] a,double[] b) { for(int i=0;i<a.Length;i++) b[i]=U.UnitConverter.Convert(a[i],UU.DensityUnit.GramPerCubicCentimeter,UU.DensityUnit.KilogramPerCubicMeter); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Engineering(double[] a,double[] b) { for(int i=0;i<a.Length;i++) b[i]=E.Density.FromGramPerCubicCentimeter(a[i]).KilogramPerCubicMeter; }
}
