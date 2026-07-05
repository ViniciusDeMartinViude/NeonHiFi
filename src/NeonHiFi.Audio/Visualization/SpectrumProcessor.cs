using NAudio.Dsp;

namespace NeonHiFi.Audio.Visualization;

/// <summary>
/// Pure, synchronous FFT + log-spaced band binning: given exactly one window
/// of samples, produces a small number of band magnitudes (dB) suitable for
/// a bar/line spectrum display. No threading here - that's deliberate, so
/// this is directly unit-testable; <see cref="SpectrumAnalyzer"/> wraps it
/// with the background thread that keeps analysis off the audio callback.
/// </summary>
public sealed class SpectrumProcessor
{
    private readonly int _fftSize;
    private readonly int _sampleRate;
    private readonly float[] _window;
    private readonly float[] _smoothedMagnitudesDb;

    public SpectrumProcessor(int sampleRate, int fftSize = 2048, int bandCount = 24, double minFrequencyHz = 20)
    {
        if ((fftSize & (fftSize - 1)) != 0)
        {
            throw new ArgumentException("fftSize must be a power of two.", nameof(fftSize));
        }

        _sampleRate = sampleRate;
        _fftSize = fftSize;
        BandCount = bandCount;
        BandEdgesHz = ComputeLogBandEdges(bandCount, minFrequencyHz, sampleRate / 2.0);
        _window = ComputeHannWindow(fftSize);

        _smoothedMagnitudesDb = new float[bandCount];
        Array.Fill(_smoothedMagnitudesDb, -80f);
    }

    public int BandCount { get; }

    /// <summary>Log-spaced band boundaries in Hz, length BandCount + 1 (edge i to edge i+1 is band i).</summary>
    public IReadOnlyList<double> BandEdgesHz { get; }

    /// <summary>
    /// Processes exactly one window of <c>fftSize</c> samples, returning
    /// per-band magnitudes in dB. Each call's result is lightly smoothed
    /// against the previous one, so a rapidly-changing signal doesn't make
    /// the display flicker frame to frame.
    /// </summary>
    public float[] Process(ReadOnlySpan<float> samples)
    {
        if (samples.Length != _fftSize)
        {
            throw new ArgumentException($"Expected exactly {_fftSize} samples, got {samples.Length}.", nameof(samples));
        }

        var complex = new Complex[_fftSize];
        for (var i = 0; i < _fftSize; i++)
        {
            complex[i].X = samples[i] * _window[i];
            complex[i].Y = 0f;
        }

        FastFourierTransform.FFT(true, (int)Math.Log2(_fftSize), complex);

        var bandSums = new double[BandCount];
        var bandBinCounts = new int[BandCount];

        // Bin 0 is DC; the loop stops before the Nyquist bin (fftSize/2) since
        // real-input FFT output above it just mirrors the lower half.
        for (var bin = 1; bin < _fftSize / 2; bin++)
        {
            var frequency = bin * (double)_sampleRate / _fftSize;
            var band = FindBand(frequency);
            if (band < 0)
            {
                continue;
            }

            var magnitude = Math.Sqrt((complex[bin].X * complex[bin].X) + (complex[bin].Y * complex[bin].Y));
            bandSums[band] += magnitude;
            bandBinCounts[band]++;
        }

        var result = new float[BandCount];
        for (var band = 0; band < BandCount; band++)
        {
            var average = bandBinCounts[band] > 0 ? bandSums[band] / bandBinCounts[band] : 0;
            var db = (float)(20 * Math.Log10(Math.Max(average, 1e-6)));

            _smoothedMagnitudesDb[band] = (_smoothedMagnitudesDb[band] * 0.6f) + (db * 0.4f);
            result[band] = _smoothedMagnitudesDb[band];
        }

        return result;
    }

    private int FindBand(double frequencyHz)
    {
        for (var band = 0; band < BandCount; band++)
        {
            if (frequencyHz >= BandEdgesHz[band] && frequencyHz < BandEdgesHz[band + 1])
            {
                return band;
            }
        }

        return -1;
    }

    private static double[] ComputeLogBandEdges(int bandCount, double minHz, double maxHz)
    {
        var edges = new double[bandCount + 1];
        var logMin = Math.Log10(minHz);
        var logMax = Math.Log10(maxHz);

        for (var i = 0; i <= bandCount; i++)
        {
            var t = (double)i / bandCount;
            edges[i] = Math.Pow(10, logMin + (t * (logMax - logMin)));
        }

        return edges;
    }

    private static float[] ComputeHannWindow(int size)
    {
        var window = new float[size];
        for (var n = 0; n < size; n++)
        {
            window[n] = (float)(0.5 * (1 - Math.Cos(2 * Math.PI * n / (size - 1))));
        }

        return window;
    }
}
