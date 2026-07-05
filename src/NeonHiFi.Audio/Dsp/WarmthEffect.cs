using NAudio.Wave;

namespace NeonHiFi.Audio.Dsp;

/// <summary>
/// A mild soft-saturation ("warmth") effect using the classic cubic
/// soft-clipper y = x - x^3/3: unity slope at x=0 (quiet signals pass
/// essentially untouched) that gently rounds off peaks as |x| approaches
/// full scale. Dry/wet blended by <see cref="Amount"/>, so 0 is an exact
/// bypass and 1 is fully wet. Disabled by default.
/// </summary>
public sealed class WarmthEffect : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly SmoothedParameter _amount;

    public WarmthEffect(ISampleProvider source)
    {
        _source = source;
        _amount = new SmoothedParameter(0f, source.WaveFormat.SampleRate);
    }

    public bool Enabled { get; set; }

    /// <summary>0 = bypass, 1 = fully saturated.</summary>
    public float Amount
    {
        get => _amount.Target;
        set => _amount.Target = value;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);
        if (!Enabled)
        {
            return samplesRead;
        }

        for (var i = 0; i < samplesRead; i++)
        {
            _amount.Advance();
            var amount = _amount.Current;
            if (amount <= 0f)
            {
                continue;
            }

            var x = buffer[offset + i];
            var clamped = Math.Clamp(x, -1f, 1f);
            var saturated = clamped - (clamped * clamped * clamped / 3f);
            buffer[offset + i] = ((1 - amount) * x) + (amount * saturated);
        }

        return samplesRead;
    }
}
