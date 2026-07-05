using System.Diagnostics;
using NAudio.Wave;
using NeonHiFi.Audio.Dsp;

namespace NeonHiFi.Audio.Tests.Dsp;

public class GraphicEqualizerTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void ExposesTenStandardBandsByDefault()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
        var eq = new GraphicEqualizer(new SineSampleProvider(1000, format));

        Assert.Equal(10, eq.Bands.Count);
        Assert.Equal(GraphicEqualizer.StandardCenterFrequencies, eq.Bands.Select(b => b.CenterFrequency));
    }

    [Fact]
    public void PassesThroughSourceWaveFormat()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        var eq = new GraphicEqualizer(new SineSampleProvider(1000, format));

        Assert.Equal(format, eq.WaveFormat);
    }

    [Fact]
    public void BoostingOneBandMatchesExpectedGainAtItsFrequency()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
        var eq = new GraphicEqualizer(new SineSampleProvider(1000, format));

        var band1k = eq.Bands.Single(b => Math.Abs(b.CenterFrequency - 1000) < 0.01);
        band1k.GainDecibels = 12f;

        var buffer = new float[SampleRate * 2];
        var total = 0;
        while (total < buffer.Length)
        {
            total += eq.Read(buffer, total, buffer.Length - total);
        }

        // Measure RMS over the last 0.5s, after smoothing/filter settling.
        var measureStart = buffer.Length - (SampleRate / 2);
        var sumSquares = 0.0;
        for (var i = measureStart; i < buffer.Length; i++)
        {
            sumSquares += buffer[i] * buffer[i];
        }

        var outRms = Math.Sqrt(sumSquares / (buffer.Length - measureStart));
        const double InRms = 0.70710678; // RMS of a unit-amplitude sine wave (1/sqrt(2))
        var measuredGain = outRms / InRms;
        var expectedGain = Math.Pow(10, 12.0 / 20);

        Assert.True(
            Math.Abs(measuredGain - expectedGain) < expectedGain * 0.2,
            $"expected ~{expectedGain}, measured {measuredGain}");
    }

    [Fact]
    public void ProcessingRunsWellWithinRealTimeBudget()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);
        var eq = new GraphicEqualizer(new ConstantSampleProvider(format, 0.5f));

        // Actively-changing gains exercise the per-sample smoothing/recompute
        // path (the expensive one), not just the cheap converged steady state.
        foreach (var band in eq.Bands)
        {
            band.GainDecibels = 6f;
        }

        var buffer = new float[4096];
        var stopwatch = Stopwatch.StartNew();
        var totalSamples = 0;
        var targetSamples = SampleRate * 2 * format.Channels; // 2 seconds of audio
        while (totalSamples < targetSamples)
        {
            totalSamples += eq.Read(buffer, 0, buffer.Length);
        }

        stopwatch.Stop();

        // Processing 2s of audio should take a small fraction of 2000ms - a
        // generous 300ms threshold (~6x margin) keeps this non-flaky on slow
        // CI runners while still catching a real regression.
        Assert.True(
            stopwatch.ElapsedMilliseconds < 300,
            $"processing 2s of audio through 10 bands took {stopwatch.ElapsedMilliseconds}ms");
    }

    private sealed class SineSampleProvider : ISampleProvider
    {
        private readonly double _phaseIncrement;
        private double _phase;

        public SineSampleProvider(double frequency, WaveFormat format)
        {
            WaveFormat = format;
            _phaseIncrement = 2 * Math.PI * frequency / format.SampleRate;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = (float)Math.Sin(_phase);
                _phase += _phaseIncrement;
            }

            return count;
        }
    }

    private sealed class ConstantSampleProvider(WaveFormat format, float value) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = format;

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = value;
            }

            return count;
        }
    }
}
