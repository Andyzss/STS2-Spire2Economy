using Godot;

namespace SpireEconomy.SpireEconomyCode.Extensions;

public static class StringExtensions
{
    public static string ImagePath(this string path)
    {
        return ResourcePath(MainFile.ResPath, "images", path);
    }

    public static string CardImagePath(this string file) =>
        Resolve(file, "card.png", "images", "card_portraits");

    public static string PowerImagePath(this string file) =>
        Resolve(file, "power.png", "images", "powers");

    public static string BigPowerImagePath(this string file) =>
        Resolve(file, "power.png", "images", "powers", "big");

    public static string RelicImagePath(this string file) =>
        Resolve(file, "relic.png", "images", "relics");

    public static string RelicOutlinePath(this string file) =>
        Resolve(file, "relic_outline.png", "images", "relics");

    public static string BigRelicImagePath(this string file) =>
        Resolve(file, "relic.png", "images", "relics", "big");

    public static string PotionImagePath(this string file) =>
        Resolve(file, "potion.png", "images", "potions");

    public static string PotionOutlinePath(this string file) =>
        Resolve(file, "potion.png", "images", "potions", "outlines");

    // A missing icon resolves to the placeholder in the same folder rather than to the path that is
    // not there. The game throws on a texture it cannot load, so one typo would take down the whole
    // screen instead of showing one wrong icon.
    private static string Resolve(string file, string fallback, params string[] dirs)
    {
        string root = ResourcePath([MainFile.ResPath, .. dirs]);
        string path = ResourcePath(root, file);
        if (ResourceLoader.Exists(path)) return path;

        MainFile.Logger.Info("Could not find image path: " + path);
        return ResourcePath(root, fallback);
    }

    private static string ResourcePath(params string[] parts)
    {
        string combined = string.Join('/', parts
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part.Trim('/')));

        const string scheme = "res://";
        if (!combined.StartsWith(scheme, StringComparison.Ordinal))
            return combined;

        // Preserve exactly two slashes after the Godot resource scheme. A blind Replace on
        // "res:/" also matches an already-correct "res://" and adds a slash on every join.
        return scheme + combined[scheme.Length..].TrimStart('/');
    }
}
