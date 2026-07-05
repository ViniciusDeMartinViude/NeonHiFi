using NAudio.Wave;

namespace NeonHiFi.Audio.Dsp;

/// <summary>
/// A dedicated bass-boost low shelf, independent of the graphic EQ's bands.
/// Disabled (<see cref="Enabled"/> = false) by default - a straight
/// passthrough, so the app works fully with just the EQ if this is never
/// turned on.
/// </summary>
public sealed class BassBoostEffect : ISampleProvider
{
    private const double ShelfCornerFrequency = 150.0;
    private const double ShelfSlope = 1.0;

    private readonly ISampleProvider _source;
    private readonly SmoothedParameter _gainDb;
    private readonly BiquadState[] _states;
    private BiquadCoefficients _coefficients;

    public BassBoostEffect(ISampleProvider source)
    {
        _source = source;
        _gainDb = new SmoothedParameter(0f, source.WaveFormat.SampleRate);
        _states = new BiquadState[source.WaveFormat.Channels];
        _coefficients = BiquadCoefficients.LowShelf(ShelfCornerFrequency, source.WaveFormat.SampleRate, 0, ShelfSlope);
    }

    public bool Enabled { get; set; }

    public float GainDecibels
    {
        get => _gainDb.Target;
        set => _gainDb.Target = value;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var samplesRead = _source.Read(buffer, offset, count);
        if (!Enabled)
        {
            return samplesRead;
        }

        var channels = WaveFormat.Channels;
        for (var i = 0; i < samplesRead; i++)
        {
            if (_gainDb.Advance())
            {
                _coefficients = BiquadCoefficients.LowShelf(ShelfCornerFrequency, WaveFormat.SampleRate, _gainDb.Current, ShelfSlope);
            }

            var channel = i % channels;
            ref var state = ref _states[channel];
            var x = buffer[offset + i];
            var y = (_coefficients.B0 * x) + (_coefficients.B1 * state.X1) + (_coefficients.B2 * state.X2)
                    - (_coefficients.A1 * state.Y1) - (_coefficients.A2 * state.Y2);

            state.X2 = state.X1;
            state.X1 = x;
            state.Y2 = state.Y1;
            state.Y1 = y;

            buffer[offset + i] = y;
        }

        return samplesRead;
    }

    private struct BiquadState
    {
        public float X1;
        public float X2;
        public float Y1;
        public float Y2;
    }
}
