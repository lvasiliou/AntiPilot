using System.Globalization;
using System.Reflection;
using Xunit;

namespace AntiPilot.Tests;

public class StringsTests
{
    /// <summary>Must match SatelliteResourceLanguages in AntiPilot.csproj.</summary>
    private static readonly string[] ShippedLanguages =
        ["ru", "es", "zh-Hans", "pt-BR", "tr", "ja", "ko", "ar", "id", "zh-Hant", "el"];

    private static IEnumerable<PropertyInfo> StringProperties => typeof(Strings)
        .GetProperties(BindingFlags.Public | BindingFlags.Static)
        .Where(property => property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0);

    [Fact]
    public void EveryGeneratedPropertyResolvesToARealResource()
    {
        // Strings.Get returns !Key! rather than throwing, so a resx that has drifted from the
        // generated accessor would otherwise show up as garbled labels at runtime instead of here.
        var missing = new List<string>();

        foreach (var property in StringProperties)
        {
            var value = (string?)property.GetValue(null);

            if (string.IsNullOrEmpty(value) || value == $"!{property.Name}!")
            {
                missing.Add(property.Name);
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void ThereIsSomethingToTranslate()
    {
        Assert.True(StringProperties.Count() > 100);
    }

    [Theory]
    [InlineData("ru")]
    [InlineData("es")]
    [InlineData("zh-Hans")]
    [InlineData("pt-BR")]
    [InlineData("tr")]
    [InlineData("ja")]
    [InlineData("ko")]
    [InlineData("ar")]
    [InlineData("id")]
    [InlineData("zh-Hant")]
    [InlineData("el")]
    public void EveryShippedLanguageFallsBackToEnglishRatherThanToNothing(string tag)
    {
        var previous = Strings.Culture;

        try
        {
            Strings.Culture = CultureInfo.GetCultureInfo(tag);

            foreach (var property in StringProperties)
            {
                var value = (string?)property.GetValue(null);
                Assert.False(string.IsNullOrEmpty(value), $"{tag}: {property.Name} resolved to nothing.");
                Assert.NotEqual($"!{property.Name}!", value);
            }
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public void ATranslationIsActuallyPresentForEachShippedLanguage()
    {
        // Guards the csproj: SatelliteResourceLanguages silently strips any language not listed,
        // and the failure mode is an app that quietly stays English.
        var previous = Strings.Culture;

        try
        {
            var english = Strings.SettingsTagline;

            foreach (var tag in ShippedLanguages)
            {
                Strings.Culture = CultureInfo.GetCultureInfo(tag);
                Assert.NotEqual(english, Strings.SettingsTagline);
            }
        }
        finally
        {
            Strings.Culture = previous;
        }
    }

    [Fact]
    public void FormatPlaceholdersSurviveTranslation()
    {
        var previous = Strings.Culture;

        try
        {
            foreach (var tag in ShippedLanguages)
            {
                Strings.Culture = CultureInfo.GetCultureInfo(tag);

                // Any translation that dropped {0} would throw here rather than in front of a user.
                Assert.Contains("7", Strings.Format(Strings.DoubleTapMilliseconds, 7));
                Assert.Contains("3", Strings.Format(Strings.PickerCount, 3));
                Assert.Contains("x", Strings.Format(Strings.TrayTooltip, "x"));
            }
        }
        finally
        {
            Strings.Culture = previous;
        }
    }
}
