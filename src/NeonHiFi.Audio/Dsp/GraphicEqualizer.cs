using NAudio.Wave;

namespace NeonHiFi.Audio.Dsp;

/// <summary>
/// A multi-band graphic EQ: cascades one <see cref="EqBand"/> per center
/// frequency in series, applied to a source <see cref="ISampleProvider"/>.
/// </summary>
public sealed class GraphicEqualizer : ISampleProvider
{
    /// <summary>ISO-standard 10-band graphic EQ center frequencies (Hz), one octave apart.</summary>
    public static readonly double[] StandardCenterFrequencies =
    [
        31.25, 62.5, 125, 250, 500, 1000, 2000, 4000, 8000, 16000,
    ];

    private readonly ISampleProvider _source;

    public GraphicEqualizer(ISampleProvider source, IReadOnlyList<double>? centerFrequencies = null)
    {
        _source = source;
        var frequencies = centerFrequencies ?? StandardCenterFrequencies;
        Bands = frequencies
            .Select(frequency => new EqBand(frequency, source.WaveFormat.SampleRate, source.WaveFormat.Channels))
            .ToArray();
    }

    public IReadOnlyList<EqBand> Bands { get; }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);
        var channels = WaveFormat.Channels;

        for (var i = 0; i < samplesRead; i++)
        {
            var channel = i % channels;
            var sample = buffer[offset + i];

            foreach (var band in Bands)
            {
                sample = band.Process(sample, channel);
            }

            buffer[offset + i] = sample;
        }

        return samplesRead;
    }
}
