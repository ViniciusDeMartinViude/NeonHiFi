namespace NeonHiFi.Audio.Dsp;

/// <summary>Normalized (a0 = 1) biquad coefficients for the difference equation
/// y[n] = b0*x[n] + b1*x[n-1] + b2*x[n-2] - a1*y[n-1] - a2*y[n-2].</summary>
public readonly struct BiquadCoefficients
{
    public BiquadCoefficients(float b0, float b1, float b2, float a1, float a2)
    {
        B0 = b0;
        B1 = b1;
        B2 = b2;
        A1 = a1;
        A2 = a2;
    }

    public float B0 { get; }

    public float B1 { get; }

    public float B2 { get; }

    public float A1 { get; }

    public float A2 { get; }

    /// <summary>
    /// RBJ Audio EQ Cookbook peaking (bell) filter: boosts/cuts a band around
    /// <paramref name="centerFrequency"/> by <paramref name="gainDb"/>, with
    /// bandwidth controlled by <paramref name="q"/>.
    /// </summary>
    public static BiquadCoefficients Peaking(double centerFrequency, double sampleRate, double gainDb, double q)
    {
        var a = Math.Pow(10, gainDb / 40);
        var w0 = 2 * Math.PI * centerFrequency / sampleRate;
        var cosW0 = Math.Cos(w0);
        var alpha = Math.Sin(w0) / (2 * q);

        var b0 = 1 + alpha * a;
        var b1 = -2 * cosW0;
        var b2 = 1 - alpha * a;
        var a0 = 1 + alpha / a;
        var a1 = -2 * cosW0;
        var a2 = 1 - alpha / a;

        return new BiquadCoefficients(
            (float)(b0 / a0), (float)(b1 / a0), (float)(b2 / a0), (float)(a1 / a0), (float)(a2 / a0));
    }

    /// <summary>
    /// RBJ Audio EQ Cookbook low-shelf filter: boosts/cuts everything below
    /// <paramref name="cornerFrequency"/> by <paramref name="gainDb"/>, flat
    /// above it. Used for a dedicated "bass boost" control, distinct from the
    /// graphic EQ's peaking bands.
    /// </summary>
    public static BiquadCoefficients LowShelf(double cornerFrequency, double sampleRate, double gainDb, double slope = 1.0)
    {
        var a = Math.Pow(10, gainDb / 40);
        var w0 = 2 * Math.PI * cornerFrequency / sampleRate;
        var cosW0 = Math.Cos(w0);
        var sinW0 = Math.Sin(w0);
        var alpha = (sinW0 / 2) * Math.Sqrt(((a + (1 / a)) * ((1 / slope) - 1)) + 2);
        var sqrtA = Math.Sqrt(a);
        var twoSqrtAAlpha = 2 * sqrtA * alpha;

        var b0 = a * (a + 1 - ((a - 1) * cosW0) + twoSqrtAAlpha);
        var b1 = 2 * a * (a - 1 - ((a + 1) * cosW0));
        var b2 = a * (a + 1 - ((a - 1) * cosW0) - twoSqrtAAlpha);
        var a0 = a + 1 + ((a - 1) * cosW0) + twoSqrtAAlpha;
        var a1 = -2 * (a - 1 + ((a + 1) * cosW0));
        var a2 = a + 1 + ((a - 1) * cosW0) - twoSqrtAAlpha;

        return new BiquadCoefficients(
            (float)(b0 / a0), (float)(b1 / a0), (float)(b2 / a0), (float)(a1 / a0), (float)(a2 / a0));
    }
}
