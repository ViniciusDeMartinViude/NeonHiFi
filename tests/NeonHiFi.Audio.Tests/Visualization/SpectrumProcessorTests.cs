using NeonHiFi.Audio.Visualization;

namespace NeonHiFi.Audio.Tests.Visualization;

public class SpectrumProcessorTests
{
    private const int SampleRate = 48000;
    private const int FftSize = 2048;

    [Theory]
    [InlineData(100.0)]
    [InlineData(1000.0)]
    [InlineData(5000.0)]
    public void TestToneProducesPeakInExpectedBand(double frequency)
    {
        var processor = new SpectrumProcessor(SampleRate, FftSize, bandCount: 24);
        var samples = GenerateSine(frequency, FftSize, SampleRate);

        // Feed the same window repeatedly so the internal smoothing settles
        // on the tone's steady-state spectrum rather than a blend with the
        // initial silence floor.
        float[] magnitudes = [];
        for (var i = 0; i < 10; i++)
        {
            magnitudes = processor.Process(samples);
        }

        var peakBandIndex = Array.IndexOf(magnitudes, magnitudes.Max());
        var expectedBandIndex = FindBandIndex(processor.BandEdgesHz, frequency);

        Assert.Equal(expectedBandIndex, peakBandIndex);
    }

    [Fact]
    public void SilenceProducesNoDistinctPeak()
    {
        var processor = new SpectrumProcessor(SampleRate, FftSize, bandCount: 24);
        var silence = new float[FftSize];

        var magnitudes = processor.Process(silence);

        // All bands should sit near the same (very low) floor - no single
        // band should tower over the others the way a real tone's does.
        var range = magnitudes.Max() - magnitudes.Min();
        Assert.True(range < 5f, $"expected near-uniform floor, got a {range}dB spread");
    }

    [Fact]
    public void ReturnsOneMagnitudePerBand()
    {
        var processor = new SpectrumProcessor(SampleRate, FftSize, bandCount: 18);
        var samples = GenerateSine(1000, FftSize, SampleRate);

        var magnitudes = processor.Process(samples);

        Assert.Equal(18, magnitudes.Length);
        Assert.Equal(19, processor.BandEdgesHz.Count);
    }

    [Fact]
    public void RejectsNonPowerOfTwoFftSize()
    {
        Assert.Throws<ArgumentException>(() => new SpectrumProcessor(SampleRate, fftSize: 1000));
    }

    [Fact]
    public void RejectsWrongWindowLength()
    {
        var processor = new SpectrumProcessor(SampleRate, FftSize);
        Assert.Throws<ArgumentException>(() => processor.Process(new float[FftSize / 2]));
    }

    private static float[] GenerateSine(double frequency, int sampleCount, int sampleRate)
    {
        var samples = new float[sampleCount];
        var phaseIncrement = 2 * Math.PI * frequency / sampleRate;
        for (var i = 0; i < sampleCount; i++)
        {
            samples[i] = (float)Math.Sin(i * phaseIncrement);
        }

        return samples;
    }

    private static int FindBandIndex(IReadOnlyList<double> bandEdgesHz, double frequencyHz)
    {
        for (var band = 0; band < bandEdgesHz.Count - 1; band++)
        {
            if (frequencyHz >= bandEdgesHz[band] && frequencyHz < bandEdgesHz[band + 1])
            {
                return band;
            }
        }

        return -1;
    }
}
