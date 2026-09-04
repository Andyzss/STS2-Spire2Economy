using Xunit;

namespace SpireEconomy.Tests;

public sealed class VanillaIsolationTests
{
    [Fact]
    public void HarmonyPatchesDoNotInterceptGlobalGoldCommands()
    {
        string patchRoot = Path.Combine(FindProjectRoot(), "SpireEconomyCode", "Patches");
        string source = string.Join('\n', Directory.GetFiles(patchRoot, "*.cs")
            .Select(File.ReadAllText));

        Assert.DoesNotContain("typeof(PlayerCmd)", source);
        Assert.DoesNotContain("nameof(PlayerCmd.GainGold)", source);
        Assert.DoesNotContain("nameof(PlayerCmd.LoseGold)", source);
        Assert.DoesNotContain("nameof(PlayerCmd.SetGold)", source);
    }

    [Fact]
    public void DebtCreationHasNoEventOrGoldHookCallSite()
    {
        string codeRoot = Path.Combine(FindProjectRoot(), "SpireEconomyCode");
        string[] callers = Directory.GetFiles(codeRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("DebtManager.cs", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("TryAddDebtAsync(", StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path)!)
            .Order()
            .ToArray();

        Assert.Equal(["LoanService.cs", "MerchantLoanPatches.cs"], callers);
    }

    private static string FindProjectRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "SpireEconomyCode")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the SpireEconomy project root.");
    }
}
