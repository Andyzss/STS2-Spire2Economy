using System.Reflection;
using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using SpireEconomy.SpireEconomyCode.Configuration;
using SpireEconomy.SpireEconomyCode.Debt;

namespace SpireEconomy.SpireEconomyCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SpireEconomy";
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        // Registers mod [ScriptPath] node scripts so scenes resolve them by res:// path
        Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(Assembly.GetExecutingAssembly());

        // SavedSpireField must exist before BaseLib finalizes its save-field registry.
        DebtCurse.RegisterSavedFields();

        // Balance settings are registered now; gameplay services are intentionally not wired in the skeleton.
        ModConfigRegistry.Register(ModId, new EconomyConfig());

        // Patch the classes one at a time. A patch that fails then disables only itself, not the whole mod
        Harmony harmony = new(ModId);
        foreach (var type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (System.Exception e)
            {
                Logger.Error($"Failed to apply Harmony patch class {type.FullName}: {e}");
            }
        }
    }
}
