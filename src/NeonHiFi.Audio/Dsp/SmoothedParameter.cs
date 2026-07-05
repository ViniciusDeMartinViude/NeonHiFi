namespace NeonHiFi.Audio.Dsp;

/// <summary>
/// A control value that ramps toward a target over ~20ms instead of jumping,
/// so live adjustments don't click (the same zipper-noise fix as EqBand,
/// shared here across the additional effects).
/// </summary>
internal sealed class SmoothedParameter
{
    private const float ConvergedThreshold = 0.001f;

    private readonly float _smoothingCoefficient;

    public SmoothedParameter(float initial, int sampleRate, float smoothingTimeMilliseconds = 20f)
    {
        Current = initial;
        Target = initial;
        _smoothingCoefficient = 1f - MathF.Exp(-1f / (sampleRate * (smoothingTimeMilliseconds / 1000f)));
    }

    public float Target { get; set; }

    public float Current { get; private set; }

    /// <summary>Nudges <see cref="Current"/> toward <see cref="Target"/> by one step. Returns whether it moved.</summary>
    public bool Advance()
    {
        if (MathF.Abs(Current - Target) <= ConvergedThreshold)
        {
            return false;
        }

        Current += (Target - Current) * _smoothingCoefficient;
        return true;
    }
}
