using NAudio.Wave;
using NeonHiFi.Audio.Dsp;

namespace NeonHiFi.Audio.Tests.Dsp;

public class StereoWidthEffectTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void DisabledByDefaultIsExactPassthrough()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);

        var reference = ReadAll(new StereoTestSource(format), 512);
        var throughEffect = ReadAll(new StereoWidthEffect(new StereoTestSource(format)), 512);

        Assert.Equal(reference, throughEffect);
    }

    [Fact]
    public void WidthOfOneIsExactIdentityWhenEnabled()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);

        var reference = ReadAll(new StereoTestSource(format), 512);
        var effect = new StereoWidthEffect(new StereoTestSource(format)) { Enabled = true, Width = 1f };
        var throughEffect = ReadAll(effect, 512);

        Assert.Equal(reference, throughEffect);
    }

    [Fact]
    public void WideningIncreasesLeftRightDifference()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);
        var effect = new StereoWidthEffect(new StereoTestSource(format)) { Enabled = true, Width = 2f };

        var buffer = ReadAll(effect, SampleRate); // let smoothing settle well past its ~20ms time constant

        var lastFrame = buffer.Length - 2;
        var left = buffer[lastFrame];
        var right = buffer[lastFrame + 1];
        var difference = MathF.Abs(left - right);

        // The test source's raw L-R difference is 1.0 (see StereoTestSource); at
        // Width=2 the side component doubles, so the difference should too.
        Assert.True(difference > 1.5f, $"expected widened L-R difference > 1.5, got {difference}");
    }

    [Fact]
    public void IsNoOpOnMonoSource()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);
        var reference = ReadAll(new StereoTestSource(format), 512);
        var effect = new StereoWidthEffect(new StereoTestSource(format)) { Enabled = true, Width = 2f };

        var throughEffect = ReadAll(effect, 512);

        Assert.Equal(reference, throughEffect);
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

    /// <summary>Constant L=1, R=0 (channel count 2) or a fixed value (mono) - an easy-to-reason-about difference signal.</summary>
    private sealed class StereoTestSource : ISampleProvider
    {
        public StereoTestSource(WaveFormat format) => WaveFormat = format;

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            var channels = WaveFormat.Channels;
            for (var i = 0; i < count; i++)
            {
                var channel = i % channels;
                buffer[offset + i] = channels == 2 ? (channel == 0 ? 1f : 0f) : 0.5f;
            }

            return count;
        }
    }
}
