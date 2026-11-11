using System.Globalization;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Read-only normal-ranged-verb analysis, not a RimWorld battle simulation.
/// Unresolved inherited cooldowns or external projectiles stay unknown rather
/// than being replaced with invented values.
/// </summary>
public static class WeaponAnalysis
{
    public sealed record WeaponProfile(
        string WeaponDef, string VerbType, string ProjectileDef,
        int BurstShots, double? ShotDamage, double? Range,
        double? Warmup, double? BurstDuration, double? Cooldown)
    {
        public double? CycleSeconds => Warmup.HasValue && BurstDuration.HasValue && Cooldown.HasValue
            ? Warmup.Value + BurstDuration.Value + Cooldown.Value : null;
        public double? TheoreticalDps => ShotDamage.HasValue && CycleSeconds is > 0
            ? ShotDamage.Value * BurstShots / CycleSeconds.Value : null;
        public double? WarmupPercentage => Fraction(Warmup);
        public double? BurstPercentage => Fraction(BurstDuration);
        public double? CooldownPercentage => Fraction(Cooldown);

        private double? Fraction(double? part) => part.HasValue && CycleSeconds is > 0
            ? Math.Clamp(part.Value * 100 / CycleSeconds.Value, 0, 100) : null;
    }

    public static IReadOnlyList<WeaponProfile> Inspect(DefDocument document, DefEntry definition)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (definition.Type != "ThingDef" || definition.Element.Document != document.Xml)
            return Array.Empty<WeaponProfile>();

        var verbs = definition.Element.Element("verbs")?.Elements("li");
        if (verbs == null) return Array.Empty<WeaponProfile>();

        var projectiles = document.Definitions()
            .Where(x => x.Type == "ThingDef" && x.DefName != "(unnamed)")
            .GroupBy(x => x.DefName)
            .ToDictionary(x => x.Key, x => x.First().Element, StringComparer.Ordinal);

        var profiles = new List<WeaponProfile>();
        var cooldown = Parse(definition.Element.Element("statBases")?.Element("RangedWeapon_Cooldown"));
        foreach (var verb in verbs)
        {
            var project = verb.Element("defaultProjectile")?.Value?.Trim() ?? "";
            var count = ParseInteger(verb.Element("burstShotCount"), 1) ?? 1;
            var gap = ParseInteger(verb.Element("ticksBetweenBurstShots"), 0);
            var burstDuration = count == 1 ? 0 : gap.HasValue ? (count - 1) * gap.Value / 60.0 : (double?)null;
            double? damage = null;
            if (projectiles.TryGetValue(project, out var projectile))
                damage = Parse(projectile.Element("projectile")?.Element("damageAmountBase"));
            profiles.Add(new WeaponProfile(
                definition.DefName,
                verb.Element("verbClass")?.Value ?? "Verb_Shoot",
                project,
                count,
                damage,
                Parse(verb.Element("range")),
                Parse(verb.Element("warmupTime")),
                burstDuration,
                cooldown));
        }
        return profiles;
    }

    private static int? ParseInteger(XElement? element, int min) =>
        element != null && int.TryParse(element.Value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var number) && number >= min ? number : null;

    private static double? Parse(XElement? element) =>
        element != null && double.TryParse(element.Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && number >= 0
                ? number : null;
}
