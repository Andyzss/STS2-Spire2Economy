using System.Text.Json;
using Xunit;

namespace SpireEconomy.Tests;

public sealed class LocalizationCoverageTests
{
    private static readonly string[] SupportedLanguages =
    [
        "deu", "eng", "esp", "fra", "ind", "ita", "jpn", "kor",
        "pol", "ptb", "rus", "spa", "tha", "tur", "zhs", "zht"
    ];

    [Fact]
    public void EveryGameLanguageHasTheSameLocalizationKeys()
    {
        string localizationRoot = FindLocalizationRoot();
        string[] tables = ["cards.json", "events.json", "gameplay_ui.json", "settings_ui.json"];

        foreach (string table in tables)
        {
            HashSet<string> englishKeys = ReadKeys(Path.Combine(localizationRoot, "eng", table));

            foreach (string language in SupportedLanguages)
            {
                string path = Path.Combine(localizationRoot, language, table);
                Assert.True(File.Exists(path), $"Missing localization table: {path}");
                Assert.Equal(englishKeys.Order(), ReadKeys(path).Order());
            }
        }
    }

    [Fact]
    public void EveryDebtTextPreservesTheDebtPlaceholder()
    {
        string localizationRoot = FindLocalizationRoot();

        foreach (string language in SupportedLanguages)
        {
            Assert.Contains("{Debt}", ReadValue(localizationRoot, language, "cards.json",
                "SPIREECONOMY-DEBT_CURSE.description"));
            Assert.Contains("{Debt}", ReadValue(localizationRoot, language, "gameplay_ui.json",
                "SPIREECONOMY-DEBT_HUD"));
            string repaymentAmount = ReadValue(localizationRoot, language, "gameplay_ui.json",
                "SPIREECONOMY-REPAY_AMOUNT");
            Assert.Contains("{Amount}", repaymentAmount);
            Assert.Contains("{Debt}", repaymentAmount);
        }
    }

    [Fact]
    public void EveryDebtCardPlacesAutomaticRemovalOnANewLine()
    {
        string localizationRoot = FindLocalizationRoot();

        foreach (string language in SupportedLanguages)
        {
            string description = ReadValue(localizationRoot, language, "cards.json",
                "SPIREECONOMY-DEBT_CURSE.description");
            Assert.Contains('\n', description);
            Assert.DoesNotContain(" NL ", description);
        }
    }

    [Fact]
    public void EveryLanguageHasBothMerchantRepaymentResponses()
    {
        string localizationRoot = FindLocalizationRoot();

        foreach (string language in SupportedLanguages)
        {
            Assert.False(string.IsNullOrWhiteSpace(ReadValue(localizationRoot, language,
                "gameplay_ui.json", "SPIREECONOMY-REPAY_NO_DEBT")));
            Assert.False(string.IsNullOrWhiteSpace(ReadValue(localizationRoot, language,
                "gameplay_ui.json", "SPIREECONOMY-REPAY_NO_GOLD")));
        }
    }

    private static HashSet<string> ReadKeys(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet();
    }

    private static string ReadValue(string root, string language, string table, string key)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, language, table)));
        return document.RootElement.GetProperty(key).GetString() ?? string.Empty;
    }

    private static string FindLocalizationRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "SpireEconomy", "localization");
            if (Directory.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SpireEconomy/localization.");
    }
}
