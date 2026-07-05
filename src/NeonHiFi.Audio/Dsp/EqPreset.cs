namespace NeonHiFi.Audio.Dsp;

/// <summary>A named set of gains, one per <see cref="GraphicEqualizer.StandardCenterFrequencies"/> band.</summary>
public sealed record EqPreset(string Name, IReadOnlyList<float> BandGains);
