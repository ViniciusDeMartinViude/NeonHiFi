using NAudio.Wave;
using NeonHiFi.Audio.Visualization;

namespace NeonHiFi.Audio.Tests.Visualization;

public class VuMeterTests
{
    private const int SampleRate = 48000;

    [Fact]
    public void TracksEachChannelIndependently()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);
        var meter = new VuMeter(new ConstantStereoSource(format, left: 0.9f, right: 0.1f));

        var buffer = new float[SampleRate * 2]; // 2s stereo, plenty to converge
        var total = 0;
        while (total < buffer.Length)
        {
            total += meter.Read(buffer, total, buffer.Length - total);
        }

        var needles = meter.GetNeedleLevels();
        Assert.Equal(2, needles.Length);
        Assert.True(needles[0] > needles[1], $"expected left ({needles[0]}) > right ({needles[1]})");
        Assert.InRange(needles[0], 0.8f, 1f);
        Assert.InRange(needles[1], 0f, 0.2f);
    }

    [Fact]
    public void PassesAudioThroughUnchanged()
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 1);

        var reference = ReadAll(new ConstantStereoSource(format, 0.5f, 0.5f), 512);
        var throughMeter = ReadAll(new VuMeter(new ConstantStereoSource(format, 0.5f, 0.5f)), 512);

        Assert.Equal(reference, throughMeter);
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

    /// <summary>Constant-amplitude source; for mono formats every sample uses <paramref name="left"/>.</summary>
    private sealed class ConstantStereoSource(WaveFormat format, float left, float right) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = format;

        public int Read(float[] buffer, int offset, int count)
        {
            var channels = WaveFormat.Channels;
            for (var i = 0; i < count; i++)
            {
                var channel = i % channels;
                buffer[offset + i] = channels == 2 ? (channel == 0 ? left : right) : left;
            }

            return count;
        }
    }
}
