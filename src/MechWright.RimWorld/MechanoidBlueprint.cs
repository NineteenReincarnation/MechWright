using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MechWright.RimWorld;

/// <summary>
/// Minimal linked skeleton for a STANDARD native mechanoid, no invented special
/// mechanics. Suggested numbers are editable, not actual RimWorld defaults.
/// Create three consistent Def XML files in a single atomic project operation.
/// </summary>
public sealed record MechanoidBlueprint(
    string DefName,
    string Label,
    string WeightClass = "Medium",
    double CombatPower = 200,
    double BodySize = 1,
    double HealthScale = 1,
    double MoveSpeed = 4.7,
    double Mass = 85,
    double SharpArmor = 0.40,
    double BluntArmor = 0.20,
    double HeatArmor = 2.0,
    int BandwidthCost = 2)
{
    public string BodyDefName => DefName + "_Body";

    public IReadOnlyDictionary<string, string> GenerateFiles()
    {
        Validate();
        static string Numeric(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
        static string Render(XElement node) => new XDocument(new XDeclaration("1.0", "utf-8", null),
            new XElement("Defs", node)).Declaration + "\n" +
            new XElement("Defs", node).ToString(SaveOptions.DisableFormatting);

        var thing = new XElement("ThingDef",
            new XAttribute("ParentName", "BaseMechanoidWalker"),
            new XAttribute("MayRequire", "Ludeon.RimWorld.Biotech"),
            new XElement("defName", DefName),
            new XElement("label", Label.Trim()),
            new XElement("statBases",
                new XElement("MoveSpeed", Numeric(MoveSpeed)),
                new XElement("Mass", Numeric(Mass)),
                new XElement("ArmorRating_Sharp", Numeric(SharpArmor)),
                new XElement("ArmorRating_Blunt", Numeric(BluntArmor)),
                new XElement("ArmorRating_Heat", Numeric(HeatArmor)),
                new XElement("BandwidthCost", BandwidthCost)),
            new XElement("race",
                new XElement("body", BodyDefName),
                new XElement("baseBodySize", Numeric(BodySize)),
                new XElement("baseHealthScale", Numeric(HealthScale)),
                new XElement("mechWeightClass", WeightClass)));

        var kind = new XElement("PawnKindDef",
            new XAttribute("ParentName", "BaseMechanoidKind"),
            new XAttribute("MayRequire", "Ludeon.RimWorld.Biotech"),
            new XElement("defName", DefName),
            new XElement("race", DefName),
            new XElement("combatPower", Numeric(CombatPower)));

        var body = new XElement("BodyDef",
            new XElement("defName", BodyDefName),
            new XElement("corePart",
                new XElement("def", "MechanicalThorax"),
                new XElement("height", "Middle"),
                new XElement("depth", "Outside")));

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [$"1.7/Defs/ThingDefs/Mechanoid/{DefName}.xml"] = Render(thing),
            [$"1.7/Defs/PawnKindDefs/{DefName}.xml"] = Render(kind),
            [$"1.7/Defs/BodyDefs/{BodyDefName}.xml"] = Render(body)
        };
    }

    public IReadOnlyList<WorkspaceFile> CreateIn(MechWorkspace project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var generated = GenerateFiles();
        foreach (var (type, name) in new[]
        {
            ("ThingDef", DefName), ("PawnKindDef", DefName), ("BodyDef", BodyDefName)
        })
        {
            if (project.FindDefinitions(type, name).Count != 0)
                throw new InvalidOperationException(
                    $"Project already defines {type}/{name}. No files were added.");
        }
        return project.AddXmlBatch(generated);
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DefName) || DefName.Length > 110 ||
            !Regex.IsMatch(DefName, @"^MP_[A-Za-z0-9_]+$", RegexOptions.CultureInvariant))
            throw new ArgumentException("DefName must start with MP_ and contain only Latin letters, numbers and underscores.");
        if (string.IsNullOrWhiteSpace(Label) || Label.Length > 160)
            throw new ArgumentException("A visible mechanoid label is required (1–160 characters).");
        if (WeightClass is not ("Light" or "Medium" or "Heavy" or "UltraHeavy"))
            throw new ArgumentException("Choose a native mechanoid weight class.");
        Check("combatPower", CombatPower, 0);
        Check("baseBodySize", BodySize, 0.0001);
        Check("baseHealthScale", HealthScale, 0.0001);
        Check("MoveSpeed", MoveSpeed, 0);
        Check("Mass", Mass, 0);
        Check("ArmorRating_Sharp", SharpArmor, 0);
        Check("ArmorRating_Blunt", BluntArmor, 0);
        Check("ArmorRating_Heat", HeatArmor, 0);
        if (BandwidthCost < 0 || BandwidthCost > 1000)
            throw new ArgumentException("BandwidthCost must be between 0 and 1000.");
    }

    private static void Check(string field, double value, double minimum)
    {
        if (!double.IsFinite(value) || value < minimum || value > 1e6)
            throw new ArgumentException(field + " is outside the supported finite range.");
    }
}
