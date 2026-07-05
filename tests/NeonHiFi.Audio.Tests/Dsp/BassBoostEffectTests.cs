using NAudio.Wave;
using NeonHiFi.Audio.Dsp;

namespace NeonHiFi.Audio.Tests.Dsp;

public class BassBoostEffectTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void DisabledByDefaultIsExactPassthrough()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);

        var reference = ReadAll(new SineSampleProvider(1000, format), 512);
        var throughEffect = ReadAll(new BassBoostEffect(new SineSampleProvider(1000, format)), 512);

        Assert.Equal(reference, throughEffect);
    }

    [Fact]
    public void BoostsLowFrequencyTowardTargetGain()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
        var effect = new BassBoostEffect(new SineSampleProvider(50, format)) // well below the 150Hz shelf corner
        {
            Enabled = true,
            GainDecibels = 12f,
        };

        var measuredGain = MeasureGain(effect, settleSamples: 96000, measureSamples: 24000);
        var expectedGain = MathF.Pow(10, 12f / 20);

        Assert.True(
            MathF.Abs(measuredGain - expectedGain) < expectedGain * 0.2f,
            $"expected ~{expectedGain}, measured {measuredGain}");
    }

    [Fact]
    public void LeavesHighFrequencyNearUnity()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
        var effect = new BassBoostEffect(new SineSampleProvider(5000, format)) // well above the shelf corner
        {
            Enabled = true,
            GainDecibels = 12f,
        };

        var measuredGain = MeasureGain(effect, settleSamples: 96000, measureSamples: 24000);

        Assert.True(MathF.Abs(measuredGain - 1f) < 0.2f, $"expected near-unity, measured {measuredGain}");
    }

    private static float MeasureGain(ISampleProvider source, int settleSamples, int measureSamples)
    {
        var settleBuffer = new float[settleSamples];
        ReadFully(source, settleBuffer);

        var measureBuffer = new float[measureSamples];
        ReadFully(source, measureBuffer);

        double inSumSquares = 0;
        double outSumSquares = 0;
        for (var i = 0; i < measureSamples; i++)
        {
            // The reference input amplitude is 1 (unit sine); compare RMS directly.
            outSumSquares += measureBuffer[i] * measureBuffer[i];
            inSumSquares += 0.5; // RMS^2 of a unit sine wave is 0.5
        }

        var inRms = Math.Sqrt(inSumSquares / measureSamples);
        var outRms = Math.Sqrt(outSumSquares / measureSamples);
        return (float)(outRms / inRms);
    }

    private static float[] ReadAll(ISampleProvider source, int count)
    {
        var buffer = new float[count];
        ReadFully(source, buffer);
        return buffer;
    }

    private static void ReadFully(ISampleProvider source, float[] buffer)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = source.Read(buffer, total, buffer.Length - total);
            if (read == 0)
            {
                break;
            }

            total += read;
        }
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
}
