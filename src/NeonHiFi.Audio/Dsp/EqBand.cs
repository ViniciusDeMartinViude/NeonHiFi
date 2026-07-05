namespace NeonHiFi.Audio.Dsp;

/// <summary>
/// One band of a graphic EQ: a peaking biquad filter per channel, sharing
/// coefficients across channels (only the delay-line state differs).
///
/// Gain changes never jump straight to the new biquad coefficients - that
/// would recompute a0/a1/a2/b0/b1/b2 against delay-line state built up under
/// the *old* coefficients, producing an audible click ("zipper noise") on
/// anything but a tiny change. Instead <see cref="GainDecibels"/> only sets a
/// target; each sample nudges an internal smoothed value toward it and
/// recomputes coefficients from that - so a fader move ramps over ~<see
/// cref="SmoothingTimeMilliseconds"/> instead of stepping. Once converged,
/// recompute is skipped entirely, so steady-state playback costs nothing
/// extra over a fixed-coefficient biquad.
/// </summary>
public sealed class EqBand
{
    private const float SmoothingTimeMilliseconds = 20f;
    private const float ConvergedThresholdDb = 0.01f;
    private const double Q = 1.41; // ~1-octave bandwidth, standard for 10-band graphic EQs at this spacing

    private readonly double _centerFrequency;
    private readonly int _sampleRate;
    private readonly float _smoothingCoefficient;
    private readonly BiquadState[] _states;

    private float _targetGainDb;
    private float _currentGainDb;
    private BiquadCoefficients _coefficients;

    public EqBand(double centerFrequency, int sampleRate, int channelCount)
    {
        _centerFrequency = centerFrequency;
        _sampleRate = sampleRate;
        _states = new BiquadState[channelCount];
        _smoothingCoefficient = 1f - MathF.Exp(-1f / (sampleRate * (SmoothingTimeMilliseconds / 1000f)));
        _coefficients = BiquadCoefficients.Peaking(centerFrequency, sampleRate, 0, Q);
    }

    public double CenterFrequency => _centerFrequency;

    public float GainDecibels
    {
        get => _targetGainDb;
        set => _targetGainDb = value;
    }

    public float Process(float sample, int channel)
    {
        if (MathF.Abs(_currentGainDb - _targetGainDb) > ConvergedThresholdDb)
        {
            _currentGainDb += (_targetGainDb - _currentGainDb) * _smoothingCoefficient;
            _coefficients = BiquadCoefficients.Peaking(_centerFrequency, _sampleRate, _currentGainDb, Q);
        }

        ref var state = ref _states[channel];
        var y0 = (_coefficients.B0 * sample) + (_coefficients.B1 * state.X1) + (_coefficients.B2 * state.X2)
                 - (_coefficients.A1 * state.Y1) - (_coefficients.A2 * state.Y2);

        state.X2 = state.X1;
        state.X1 = sample;
        state.Y2 = state.Y1;
        state.Y1 = y0;

        return y0;
    }

    private struct BiquadState
    {
        public float X1;
        public float X2;
        public float Y1;
        public float Y2;
    }
}
