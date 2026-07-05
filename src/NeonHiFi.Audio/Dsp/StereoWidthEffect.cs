using NAudio.Wave;

namespace NeonHiFi.Audio.Dsp;

/// <summary>
/// Widens (or narrows) the stereo image via mid-side scaling: splits L/R into
/// Mid = (L+R)/2 and Side = (L-R)/2, scales Side by <see cref="Width"/>, then
/// recombines. Width = 1 is an exact identity transform (verified in tests),
/// so this is transparent until deliberately adjusted. Disabled by default,
/// and a no-op on anything but stereo (mono has no image to widen).
/// </summary>
public sealed class StereoWidthEffect : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly SmoothedParameter _width;

    public StereoWidthEffect(ISampleProvider source)
    {
        _source = source;
        _width = new SmoothedParameter(1f, source.WaveFormat.SampleRate);
    }

    public bool Enabled { get; set; }

    /// <summary>1 = unchanged, 0 = mono, &gt;1 = wider.</summary>
    public float Width
    {
        get => _width.Target;
        set => _width.Target = value;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);
        if (!Enabled || WaveFormat.Channels != 2)
        {
            return samplesRead;
        }

        for (var i = 0; i + 1 < samplesRead; i += 2)
        {
            _width.Advance();

            var left = buffer[offset + i];
            var right = buffer[offset + i + 1];
            var mid = (left + right) * 0.5f;
            var side = (left - right) * 0.5f * _width.Current;

            buffer[offset + i] = mid + side;
            buffer[offset + i + 1] = mid - side;
        }

        return samplesRead;
    }
}
