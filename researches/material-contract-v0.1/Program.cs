using Gravia.MaterialContract.Reference;

int checks = 0;
void Near(double actual, double expected, double tolerance = 1e-10)
{
    if (!double.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
        throw new Exception($"Expected {expected:R}, got {actual:R}");
    checks++;
}
void Reject(Action action)
{
    try { action(); } catch (ArgumentException) { checks++; return; }
    throw new Exception("Expected invalid input to be rejected.");
}

Near(Conversions.CelsiusToKelvin(489), 762.15);
Near(Conversions.KelvinToCelsius(762.15), 489);
Near(Conversions.KmolHToMolS(3384), 940);
Near(Conversions.MolSToKmolH(940), 3384);
Near(Conversions.TonnesHToKgS(360), 100);
Near(Conversions.GPerMlToKgPerM3(0.8), 800);
Near(Conversions.MassPercentToFraction(1.2), 0.012);
Near(Conversions.MassPpmToFraction(10), 0.00001);

var period = Conversions.CumulativeCatalystKg([3220, 7480, 18620]);
Near(period[0], 3220); Near(period[1], 10700); Near(period[2], 29320);

// Synthetic arithmetic example, not a claim about chemistry.
double[] molarMass = [0.1, 0.002];
double[] mole = [0.3, 0.7];
var mass = Conversions.MoleToMass(mole, molarMass);
var modelInput = Conversions.ToReformingInput(2, mass, molarMass);
Near(modelInput.MoleFractions[0], mole[0]);
Near(modelInput.MoleFractions[1], mole[1]);
var recoveredFlow = Conversions.KmolHToMolS(modelInput.F0KmolH)
    * mole.Select((x, i) => x * molarMass[i]).Sum();
Near(recoveredFlow, 2);

double[] w = [.1,.1,.1,.025,.025,.025,.025,.02,.1,.2,.03,.15,.02,.02,.02,.04];
foreach (double feed in new[] { 1.0, 100.0, 1250.0 })
{
    var products = Conversions.FccProducts(feed, w);
    Near(products.Values.Sum(p => p.MassFlowKgS), feed);
    Near(products["gasoline"].MassFlowKgS, feed * .5);
    foreach (var p in products.Values) Near(p.MassFractions!.Sum(), 1);
    double[] expected = [.2, .4, .06, .04, .3];
    var piona = Conversions.FccGasolinePiona(products["gasoline"]);
    for (int i = 0; i < 5; i++) Near(piona[i], expected[i]);
}
if (Conversions.FccProducts(0, w).Values.Any(p => p.MassFractions is not null))
    throw new Exception("Zero flow must allow undefined composition.");
checks++;
Reject(() => Conversions.FccGasolinePiona(Conversions.FccProducts(0, w)["gasoline"]));
Reject(() => Conversions.FccProducts(1, [1, double.NaN]));
Reject(() => Conversions.ValidateFractions([.3, .3]));

double[] n38 = Enumerable.Range(1, 38).Select(i => (double)i).ToArray();
double[] mm38 = Enumerable.Range(1, 38).Select(i => 10.0 + i).ToArray();
var mass38 = Conversions.ReformingComponentMassFlows(n38, mm38);
var reformate = Conversions.SelectProduct(mass38, Enumerable.Range(5, 32).ToArray());
var offgas = Conversions.SelectProduct(mass38, [0, 1, 2, 3, 4, 37]);
Near(reformate.MassFlowKgS + offgas.MassFlowKgS, mass38.Sum());
Near(reformate.MassFractions!.Sum(), 1);
Near(offgas.MassFractions!.Sum(), 1);
Near(Conversions.AdditiveVolumeDensity([.5, .5], [800, 1000]), 888.8888888888889);

Console.WriteLine($"PASS: {checks} arithmetic/contract checks. No kinetic model executed.");
