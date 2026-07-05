using NeonHiFi.Audio.Visualization;

namespace NeonHiFi.Audio.Tests.Visualization;

public class VuBallisticsTests
{
    [Fact]
    public void NeedleDoesNotJumpInstantlyOnStepInput()
    {
        var vu = new VuBallistics();

        vu.Update(1f, 0.001f); // a single, very short step

        Assert.True(vu.NeedleLevel < 0.1f, $"expected the needle to barely move after 1ms, got {vu.NeedleLevel}");
    }

    [Fact]
    public void NeedleReachesRoughlyTwoThirdsAfterOneTimeConstant()
    {
        var vu = new VuBallistics();

        // Step to 1.0, advance in small increments totaling one time constant (~120ms).
        AdvanceSeconds(vu, 1f, 0.12f);

        // A first-order exponential response reaches ~63% after one time
        // constant - allow a reasonably wide band around that.
        Assert.InRange(vu.NeedleLevel, 0.55f, 0.72f);
    }

    [Fact]
    public void NeedleFullyConvergesAfterSeveralTimeConstants()
    {
        var vu = new VuBallistics();

        AdvanceSeconds(vu, 1f, 0.6f); // ~5 time constants

        Assert.True(vu.NeedleLevel > 0.98f, $"expected near-full convergence, got {vu.NeedleLevel}");
    }

    [Fact]
    public void NeedleReleasesGraduallyNotInstantly()
    {
        var vu = new VuBallistics();
        AdvanceSeconds(vu, 1f, 0.6f); // converge to steady-state at 1.0

        vu.Update(0f, 0.001f); // instant drop to silence, single short step

        Assert.True(vu.NeedleLevel > 0.9f, $"expected the needle to barely have moved after 1ms, got {vu.NeedleLevel}");
    }

    [Fact]
    public void SmoothsOutRapidlyJitteringInput()
    {
        var vu = new VuBallistics();

        // Simulate raw per-sample-ish jitter: alternating between 0 and 1
        // every 2ms, far faster than the ~300ms time constant. Run for ~1s
        // (several time constants) so the needle has time to converge
        // toward the jittering signal's average.
        for (var i = 0; i < 500; i++)
        {
            vu.Update(i % 2 == 0 ? 1f : 0f, 0.002f);
        }

        // The raw input swings across the full 0-1 range every step; the
        // smoothed needle should have settled to a small range around the
        // input's average (~0.5), not still swinging wildly.
        Assert.InRange(vu.NeedleLevel, 0.3f, 0.7f);
    }

    [Fact]
    public void PeakHoldsAtMaximumBeforeDecaying()
    {
        var vu = new VuBallistics();

        vu.Update(1f, 0.01f); // a brief peak
        vu.Update(0f, 0.01f); // then silence

        Assert.Equal(1f, vu.PeakLevel);

        // Still within the ~1.5s hold window - shouldn't have started decaying yet.
        AdvanceSeconds(vu, 0f, 1.0f);
        Assert.Equal(1f, vu.PeakLevel);
    }

    [Fact]
    public void PeakDecaysAfterHoldWindowExpires()
    {
        var vu = new VuBallistics();

        vu.Update(1f, 0.01f);
        vu.Update(0f, 0.01f);

        AdvanceSeconds(vu, 0f, 3.0f); // well past the ~1.5s hold + some decay time

        Assert.True(vu.PeakLevel < 1f, $"expected the peak to have decayed by now, got {vu.PeakLevel}");
    }

    [Fact]
    public void PeakTracksNewHigherLevelsImmediately()
    {
        var vu = new VuBallistics();

        vu.Update(0.3f, 0.01f);
        Assert.Equal(0.3f, vu.PeakLevel);

        vu.Update(0.8f, 0.01f);
        Assert.Equal(0.8f, vu.PeakLevel);
    }

    private static void AdvanceSeconds(VuBallistics vu, float instantaneousLevel, float totalSeconds)
    {
        const float StepSeconds = 0.001f;
        var elapsed = 0f;
        while (elapsed < totalSeconds)
        {
            vu.Update(instantaneousLevel, StepSeconds);
            elapsed += StepSeconds;
        }
    }
}
