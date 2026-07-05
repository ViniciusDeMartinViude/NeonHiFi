namespace NeonHiFi.Audio.Visualization;

/// <summary>
/// Time-constant-based level smoothing mimicking an analog VU meter's needle:
/// it can't jump instantly to a new level, it ramps toward it over ~300ms
/// (the classic VU standard), symmetric for rises and falls - this is what
/// makes needle movement read as smooth rather than a jittery raw level.
///
/// A separate peak-hold value latches onto the highest recent instantaneous
/// level, holds there briefly, then decays - the small peak marker real
/// meters and mixing consoles show alongside the main needle.
///
/// Pure and synchronous (no threading, no audio-format knowledge) so it's
/// directly unit-testable; <see cref="VuMeter"/> feeds it real per-block RMS
/// levels from the audio pipeline.
/// </summary>
public sealed class VuBallistics
{
    private const float AttackReleaseTimeConstantSeconds = 0.3f;
    private const float PeakHoldSeconds = 1.5f;
    private const float PeakDecayPerSecond = 1.0f;

    public float NeedleLevel { get; private set; }

    public float PeakLevel { get; private set; }

    private float _peakHoldRemainingSeconds;

    /// <summary>Advances the ballistics given a new instantaneous level (0-1 linear) and elapsed time.</summary>
    public void Update(float instantaneousLevel, float deltaTimeSeconds)
    {
        instantaneousLevel = Math.Clamp(instantaneousLevel, 0f, 1f);

        var alpha = 1f - MathF.Exp(-deltaTimeSeconds / AttackReleaseTimeConstantSeconds);
        NeedleLevel += (instantaneousLevel - NeedleLevel) * alpha;

        if (instantaneousLevel >= PeakLevel)
        {
            PeakLevel = instantaneousLevel;
            _peakHoldRemainingSeconds = PeakHoldSeconds;
        }
        else if (_peakHoldRemainingSeconds > 0)
        {
            _peakHoldRemainingSeconds -= deltaTimeSeconds;
        }
        else
        {
            PeakLevel = Math.Max(0f, PeakLevel - (PeakDecayPerSecond * deltaTimeSeconds));
        }
    }
}
