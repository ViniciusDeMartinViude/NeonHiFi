using NeonHiFi.Audio.Dsp;

namespace NeonHiFi.Audio.Tests.Dsp;

public class EqBandTests
{
    private const int SampleRate = 48000;

    [Theory]
    [InlineData(1000, 6.0f)]
    [InlineData(1000, -6.0f)]
    [InlineData(1000, 12.0f)]
    public void GainAtCenterFrequencyMatchesTargetLinearGain(double centerFrequency, float gainDb)
    {
        var band = new EqBand(centerFrequency, SampleRate, channelCount: 1);
        band.GainDecibels = gainDb;

        var source = new SineSource(centerFrequency, SampleRate);
        RunSamples(band, source, 96000); // let smoothing + filter settle (~2s)
        var measuredGain = MeasureGain(band, source, 24000);

        var expectedGain = MathF.Pow(10, gainDb / 20);
        Assert.True(
            MathF.Abs(measuredGain - expectedGain) < expectedGain * 0.15f,
            $"expected ~{expectedGain}, measured {measuredGain}");
    }

    [Fact]
    public void GainFarFromCenterFrequencyStaysNearUnity()
    {
        var band = new EqBand(centerFrequency: 1000, SampleRate, channelCount: 1)
        {
            GainDecibels = 12f,
        };

        var source = new SineSource(4000, SampleRate); // two octaves away
        RunSamples(band, source, 96000);
        var measuredGain = MeasureGain(band, source, 24000);

        Assert.True(MathF.Abs(measuredGain - 1f) < 0.3f, $"expected near-unity, measured {measuredGain}");
    }

    [Theory]
    [InlineData(100.0)]
    [InlineData(1000.0)]
    [InlineData(8000.0)]
    public void ZeroGainIsUnityAtAnyFrequency(double frequency)
    {
        var band = new EqBand(centerFrequency: 1000, SampleRate, channelCount: 1);

        var source = new SineSource(frequency, SampleRate);
        RunSamples(band, source, 4000);
        var measuredGain = MeasureGain(band, source, 24000);

        Assert.True(MathF.Abs(measuredGain - 1f) < 0.05f, $"freq={frequency}: expected ~1.0, measured {measuredGain}");
    }

    [Fact]
    public void GainChangeRampsGraduallyInsteadOfJumping()
    {
        var band = new EqBand(centerFrequency: 1000, SampleRate, channelCount: 1);
        var source = new SineSource(1000, SampleRate);
        RunSamples(band, source, 2000); // trivial settle at the default 0dB

        band.GainDecibels = 12f;

        // Measured right as the ramp begins (~10ms of samples, well under the
        // ~20ms smoothing time constant) - should be well below the eventual
        // converged gain if it's actually ramping rather than jumping.
        var immediateGain = MeasureGain(band, source, 480);

        RunSamples(band, source, 96000); // let it fully converge
        var convergedGain = MeasureGain(band, source, 24000);

        Assert.True(
            immediateGain < convergedGain * 0.85f,
            $"expected still-ramping gain ({immediateGain}) well below converged gain ({convergedGain})");
    }

    private static float MeasureGain(EqBand band, SineSource source, int sampleCount)
    {
        double inSumSquares = 0;
        double outSumSquares = 0;

        for (var i = 0; i < sampleCount; i++)
        {
            var x = source.Next();
            var y = band.Process(x, 0);
            inSumSquares += x * x;
            outSumSquares += y * y;
        }

        var inRms = Math.Sqrt(inSumSquares / sampleCount);
        var outRms = Math.Sqrt(outSumSquares / sampleCount);

        return (float)(outRms / inRms);
    }

    private static void RunSamples(EqBand band, SineSource source, int sampleCount)
    {
        for (var i = 0; i < sampleCount; i++)
        {
            band.Process(source.Next(), 0);
        }
    }

    private sealed class SineSource(double frequency, int sampleRate)
    {
        private readonly double _phaseIncrement = 2 * Math.PI * frequency / sampleRate;
        private double _phase;

        public float Next()
        {
            var value = (float)Math.Sin(_phase);
            _phase += _phaseIncrement;
            return value;
        }
    }
}
