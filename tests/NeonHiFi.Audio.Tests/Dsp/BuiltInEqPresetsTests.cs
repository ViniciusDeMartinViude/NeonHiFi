using NeonHiFi.Audio.Dsp;

namespace NeonHiFi.Audio.Tests.Dsp;

public class BuiltInEqPresetsTests
{
    [Theory]
    [MemberData(nameof(AllBuiltInPresets))]
    public void EachPresetHasOneGainPerStandardBand(EqPreset preset)
    {
        Assert.Equal(GraphicEqualizer.StandardCenterFrequencies.Length, preset.BandGains.Count);
    }

    [Fact]
    public void FlatPresetIsAllZeroGain()
    {
        Assert.All(BuiltInEqPresets.Flat.BandGains, gain => Assert.Equal(0f, gain));
    }

    [Theory]
    [InlineData("Flat")]
    [InlineData("flat")]
    [InlineData("ROCK")]
    [InlineData("Bass Boost")]
    [InlineData("vocal")]
    public void ReservedNamesAreRecognizedCaseInsensitively(string name)
    {
        Assert.True(BuiltInEqPresets.IsReservedName(name));
    }

    [Theory]
    [InlineData("My Custom Preset")]
    [InlineData("")]
    [InlineData("Flatt")]
    public void NonReservedNamesAreNotFlagged(string name)
    {
        Assert.False(BuiltInEqPresets.IsReservedName(name));
    }

    [Fact]
    public void AllContainsExactlyTheFourNamedPresets()
    {
        var names = BuiltInEqPresets.All.Select(p => p.Name).ToArray();
        Assert.Equal(["Flat", "Rock", "Bass Boost", "Vocal"], names);
    }

    public static IEnumerable<object[]> AllBuiltInPresets() =>
        BuiltInEqPresets.All.Select(preset => new object[] { preset });
}
