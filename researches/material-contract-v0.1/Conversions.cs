namespace Gravia.MaterialContract.Reference;

public sealed record Product(double MassFlowKgS, double[]? MassFractions);
public sealed record ReformingInput(double F0KmolH, double[] MoleFractions);

public static class Conversions
{
    public static double CelsiusToKelvin(double value) => value + 273.15;
    public static double KelvinToCelsius(double value) => value - 273.15;
    public static double KmolHToMolS(double value) => value / 3.6;
    public static double MolSToKmolH(double value) => value * 3.6;
    public static double TonnesHToKgS(double value) => value / 3.6;
    public static double GPerMlToKgPerM3(double value) => value * 1000;
    public static double MassPercentToFraction(double value) => value / 100;
    public static double MassPpmToFraction(double value) => value * 1e-6;

    public static void ValidateFractions(double[] fractions, double sumTolerance = 1e-10)
    {
        if (fractions.Length == 0 || fractions.Any(v => !double.IsFinite(v) || v < 0 || v > 1)
            || Math.Abs(fractions.Sum() - 1) > sumTolerance)
            throw new ArgumentException("Expected finite, nonnegative, normalized fractions.");
    }

    private static void ValidateMolarMasses(double[] fractions, double[] masses)
    {
        ValidateFractions(fractions);
        if (fractions.Length != masses.Length || masses.Any(m => !double.IsFinite(m) || m <= 0))
            throw new ArgumentException("Positive molar masses required for every component.");
    }

    public static double[] MoleToMass(double[] moleFractions, double[] molarMassKgMol)
    {
        ValidateMolarMasses(moleFractions, molarMassKgMol);
        var masses = moleFractions.Select((x, i) => x * molarMassKgMol[i]).ToArray();
        var total = masses.Sum();
        return masses.Select(m => m / total).ToArray();
    }

    public static ReformingInput ToReformingInput(double massFlowKgS, double[] w, double[] molarMassKgMol)
    {
        ValidateMolarMasses(w, molarMassKgMol);
        if (!double.IsFinite(massFlowKgS) || massFlowKgS <= 0)
            throw new ArgumentException("Reforming calculation requires positive feed flow.");
        var n = w.Select((v, i) => massFlowKgS * v / molarMassKgMol[i]).ToArray();
        var total = n.Sum();
        return new(MolSToKmolH(total), n.Select(v => v / total).ToArray());
    }

    public static double[] CumulativeCatalystKg(double[] individualMassKg)
    {
        if (individualMassKg.Length == 0 || individualMassKg.Any(v => !double.IsFinite(v) || v <= 0))
            throw new ArgumentException("Positive catalyst load required per reactor.");
        var result = new double[individualMassKg.Length];
        double cumulative = 0;
        for (int i = 0; i < result.Length; i++) result[i] = cumulative += individualMassKg[i];
        return result;
    }

    // FCC indices follow the provided model's LUMPS, zero-based.
    public static IReadOnlyDictionary<string, Product> FccProducts(double feedKgS, double[] fractions)
    {
        ValidateFractions(fractions);
        if (fractions.Length != 16 || !double.IsFinite(feedKgS) || feedKgS < 0)
            throw new ArgumentException("Expected FCC-16 and nonnegative feed flow.");
        var componentFlows = fractions.Select(v => feedKgS * v).ToArray();
        return new Dictionary<string, Product>
        {
            ["gasoline"] = SelectProduct(componentFlows, [7, 8, 9, 10, 11]),
            ["lco"] = SelectProduct(componentFlows, [3, 4, 5, 6]),
            ["gas"] = SelectProduct(componentFlows, [12, 13, 14]),
            ["coke"] = SelectProduct(componentFlows, [15]),
            ["heavyResidue"] = SelectProduct(componentFlows, [0, 1, 2])
        };
    }

    public static Product SelectProduct(double[] componentFlowsKgS, int[] indices)
    {
        if (indices.Length == 0 || indices.Distinct().Count() != indices.Length
            || indices.Any(i => i < 0 || i >= componentFlowsKgS.Length)
            || componentFlowsKgS.Any(v => !double.IsFinite(v) || v < 0))
            throw new ArgumentException("Invalid component flows or selection.");
        var selected = indices.Select(i => componentFlowsKgS[i]).ToArray();
        double total = selected.Sum();
        return new(total, total == 0 ? null : selected.Select(v => v / total).ToArray());
    }

    public static double[] FccGasolinePiona(Product gasoline)
    {
        if (gasoline.MassFractions is not { Length: 5 } w)
            throw new ArgumentException("Nonempty FCC gasoline composition required.");
        // Native order Nl,nPl,iPl,Ol,Al -> P,I,O,N,A.
        return [w[1], w[2], w[3], w[0], w[4]];
    }

    public static double[] ReformingComponentMassFlows(double[] flowKmolH, double[] mmKgKmol)
    {
        if (flowKmolH.Length != 38 || mmKgKmol.Length != 38
            || flowKmolH.Any(v => !double.IsFinite(v) || v < 0)
            || mmKgKmol.Any(v => !double.IsFinite(v) || v <= 0))
            throw new ArgumentException("Expected 38 component flows and molar masses.");
        return flowKmolH.Select((n, i) => n * mmKgKmol[i] / 3600).ToArray();
    }

    public static double AdditiveVolumeDensity(double[] w, double[] densitiesKgM3)
    {
        ValidateFractions(w);
        if (w.Length != densitiesKgM3.Length || densitiesKgM3.Any(v => !double.IsFinite(v) || v <= 0))
            throw new ArgumentException("Positive densities at the same reference conditions required.");
        return 1 / w.Select((v, i) => v / densitiesKgM3[i]).Sum();
    }
}
