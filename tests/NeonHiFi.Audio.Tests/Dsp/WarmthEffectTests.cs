using NAudio.Wave;
using NeonHiFi.Audio.Dsp;

namespace NeonHiFi.Audio.Tests.Dsp;

public class WarmthEffectTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void DisabledByDefaultIsExactPassthrough()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);

        var reference = ReadAll(new SineSampleProvider(1000, format), 512);
        var throughEffect = ReadAll(new WarmthEffect(new SineSampleProvider(1000, format)), 512);

        Assert.Equal(reference, throughEffect);
    }

    [Fact]
    public void ZeroAmountIsExactPassthroughWhenEnabled()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);

        var reference = ReadAll(new SineSampleProvider(1000, format), 512);
        var effect = new WarmthEffect(new SineSampleProvider(1000, format)) { Enabled = true, Amount = 0f };
        var throughEffect = ReadAll(effect, 512);

        Assert.Equal(reference, throughEffect);
    }

    [Fact]
    public void FullAmountCompressesPeaksRelativeToLinearInput()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
        var effect = new WarmthEffect(new SineSampleProvider(1000, format, amplitude: 0.9f))
        {
            Enabled = true,
            Amount = 1f,
        };

        // Let the amount ramp fully settle, then measure peak amplitude.
        ReadAll(effect, 96000);
        var buffer = ReadAll(effect, 4800); // ~100ms, several full cycles at 1kHz

        var inputPeak = 0.9f;
        var outputPeak = buffer.Max(MathF.Abs);

        // Soft-clipping saturation compresses peaks below the linear input
        // level - if it weren't doing anything, outputPeak would equal
        // inputPeak (within FP tolerance).
        Assert.True(outputPeak < inputPeak - 0.01f, $"expected compressed peak < {inputPeak}, got {outputPeak}");
    }

    private static float[] ReadAll(ISampleProvider source, int count)
    {
        var buffer = new float[count];
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

        return buffer;
    }

    private sealed class SineSampleProvider : ISampleProvider
    {
        private readonly double _phaseIncrement;
        private readonly float _amplitude;
        private double _phase;

        public SineSampleProvider(double frequency, WaveFormat format, float amplitude = 1f)
        {
            WaveFormat = format;
            _amplitude = amplitude;
            _phaseIncrement = 2 * Math.PI * frequency / format.SampleRate;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            for (var i = 0; i < count; i++)
            {
                buffer[offset + i] = _amplitude * (float)Math.Sin(_phase);
                _phase += _phaseIncrement;
            }

            return count;
        }
    }
}
