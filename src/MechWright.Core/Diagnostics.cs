namespace MechWright.Core;

public enum Severity { Information, Warning, Error }

public sealed record Diagnostic(
    string Code,
    Severity Severity,
    string Message,
    string Location)
{
    public override string ToString() => $"[{Severity}] {Code}: {Message} — {Location}";
}

public sealed record DefinitionIdentity(string Type, string DefName, string Source);

public sealed record CatalogEntry(
    DefinitionIdentity Identity,
    string Label,
    string FilePath,
    string SourcePackage);

public enum FieldOrigin { Inherited, Explicit, Suggested, Unavailable, UnknownRaw }
