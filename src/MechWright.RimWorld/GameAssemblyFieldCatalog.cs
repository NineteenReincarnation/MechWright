using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace MechWright.RimWorld;

/// <summary>
/// Reads only ECMA-335 metadata. Never loads, resolves or executes a game
/// assembly, and never writes to the install directory. A C# field is a
/// candidate, NOT proof that RimWorld's XML loader accepts a given tag.
/// </summary>
public sealed class GameAssemblyFieldCatalog
{
    public sealed record Field(
        string DefType, string Path, string Kind, string TypeName,
        IReadOnlyList<string> Choices, string DeclaringType)
    {
        public string Name => Path.Split('/')[^1];
    }

    private readonly IReadOnlyDictionary<string, IReadOnlyList<Field>> _fields;
    private GameAssemblyFieldCatalog(IReadOnlyDictionary<string, IReadOnlyList<Field>> fields) =>
        _fields = fields;

    public static GameAssemblyFieldCatalog Empty { get; } =
        new(new Dictionary<string, IReadOnlyList<Field>>(StringComparer.Ordinal));

    public IReadOnlyList<Field> For(string defType) =>
        _fields.GetValueOrDefault(defType) ?? [];

    public int Count => _fields.Values.Sum(x => x.Count);

    public static GameAssemblyFieldCatalog Read(string assemblyPath,
        CancellationToken token = default)
    {
        var file = new FileInfo(assemblyPath);
        if (!file.Exists || file.Length is < 1024 or > 160_000_000)
            throw new InvalidDataException("游戏程序集不存在，或其大小不在允许的读取范围。");
        using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read,
            FileShare.Read, 65536, FileOptions.SequentialScan);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (!pe.HasMetadata)
            throw new BadImageFormatException("所选文件不是 .NET 程序集。");
        var reader = pe.GetMetadataReader();
        if (!reader.IsAssembly)
            throw new BadImageFormatException("所选文件不包含程序集元数据。");

        // Only the named game Def types are currently exposed in the normal
        // Inspector. Other types remain available in the advanced XML view.
        string[] known = ["ThingDef", "PawnKindDef", "BodyDef", "BodyPartDef",
            "BodyPartGroupDef", "RecipeDef", "AbilityDef", "HediffDef",
            "DamageDef", "EffecterDef", "SoundDef", "StatDef"];

        var byName = new Dictionary<string, TypeDefinitionHandle>(StringComparer.Ordinal);
        var byShortName = new Dictionary<string, TypeDefinitionHandle>(StringComparer.Ordinal);
        foreach (var handle in reader.TypeDefinitions)
        {
            token.ThrowIfCancellationRequested();
            var type = reader.GetTypeDefinition(handle);
            var name = reader.GetString(type.Name);
            var ns = reader.GetString(type.Namespace);
            byName.TryAdd(ns.Length == 0 ? name : ns + "." + name, handle);
            byShortName.TryAdd(name, handle);
        }
        var signature = new MetadataTypeNames();
        var outputs = new Dictionary<string, IReadOnlyList<Field>>(StringComparer.Ordinal);
        foreach (var defType in known)
        {
            if (!byShortName.TryGetValue(defType, out var root)) continue;
            var result = new List<Field>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Collect(reader, root, defType, "", 0, result, seen, byName, signature, token);
            outputs[defType] = result.OrderBy(x => x.Path, StringComparer.Ordinal).ToArray();
        }
        return new GameAssemblyFieldCatalog(outputs);
    }

    private static void Collect(MetadataReader reader, TypeDefinitionHandle root,
        string defType, string prefix, int depth, List<Field> output,
        HashSet<string> seen, Dictionary<string, TypeDefinitionHandle> types,
        MetadataTypeNames signatures, CancellationToken token)
    {
        if (depth > 2 || output.Count >= 2400) return;
        var current = root;
        var visited = new HashSet<TypeDefinitionHandle>();
        while (!current.IsNil && visited.Add(current))
        {
            token.ThrowIfCancellationRequested();
            var info = reader.GetTypeDefinition(current);
            var declaring = FullName(reader.GetString(info.Namespace),
                reader.GetString(info.Name));
            foreach (var handle in info.GetFields())
            {
                if (output.Count >= 2400) break;
                var field = reader.GetFieldDefinition(handle);
                if ((field.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public ||
                    (field.Attributes & (FieldAttributes.Static | FieldAttributes.Literal |
                        FieldAttributes.NotSerialized | FieldAttributes.SpecialName)) != 0)
                    continue;
                var fieldName = reader.GetString(field.Name);
                if (!IsLegalXmlName(fieldName) || fieldName is "defName" or "label" ||
                    fieldName.Contains("k__BackingField", StringComparison.Ordinal)) continue;
                var path = prefix + fieldName;
                if (!seen.Add(path)) continue;
                string typeName;
                try { typeName = field.DecodeSignature(signatures, null); }
                catch (BadImageFormatException) { continue; }
                var kind = Classify(typeName, reader, types, out var choices);
                if (kind != null)
                {
                    output.Add(new Field(defType, path, kind, typeName,
                        choices, declaring));
                    continue;
                }
                // Explicitly recognize only known simple object branches.
                // Lists, polymorphic comp collections and unknown classes are
                // never flattened into guessed scalar XML paths.
                if (depth != 0 || fieldName is not ("race" or "graphicData")) continue;
                if (!types.TryGetValue(typeName, out var nested)) continue;
                Collect(reader, nested, defType, path + "/", depth + 1,
                    output, seen, types, signatures, token);
            }
            current = ResolveBase(reader, info.BaseType, types);
        }
    }

    private static TypeDefinitionHandle ResolveBase(MetadataReader reader,
        EntityHandle handle, Dictionary<string, TypeDefinitionHandle> types)
    {
        if (handle.Kind == HandleKind.TypeDefinition)
            return (TypeDefinitionHandle)handle;
        if (handle.Kind != HandleKind.TypeReference) return default;
        var type = reader.GetTypeReference((TypeReferenceHandle)handle);
        return types.GetValueOrDefault(FullName(reader.GetString(type.Namespace),
            reader.GetString(type.Name)));
    }

    private static string? Classify(string type, MetadataReader reader,
        Dictionary<string, TypeDefinitionHandle> types, out string[] choices)
    {
        choices = [];
        switch (type)
        {
            case "System.Boolean": return "Boolean";
            case "System.Single":
            case "System.Double":
            case "System.Decimal": return "Number";
            case "System.Byte":
            case "System.SByte":
            case "System.Int16":
            case "System.UInt16":
            case "System.Int32":
            case "System.UInt32":
            case "System.Int64":
            case "System.UInt64": return "Integer";
            case "System.String": return "Text";
        }
        if (!types.TryGetValue(type, out var handle)) return null;
        var def = reader.GetTypeDefinition(handle);
        if (ResolveTypeName(reader, def.BaseType) != "System.Enum")
            return null;
        choices = def.GetFields().Select(reader.GetFieldDefinition)
            .Where(x => (x.Attributes & FieldAttributes.Literal) != 0 &&
                        (x.Attributes & FieldAttributes.Static) != 0)
            .Select(x => reader.GetString(x.Name))
            .Where(x => x != "value__")
            .Distinct(StringComparer.Ordinal).Take(512).ToArray();
        return choices.Length == 0 ? null : "Enum";
    }

    private static string ResolveTypeName(MetadataReader reader, EntityHandle handle)
    {
        if (handle.Kind == HandleKind.TypeDefinition)
        {
            var type = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
            return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }
        if (handle.Kind == HandleKind.TypeReference)
        {
            var type = reader.GetTypeReference((TypeReferenceHandle)handle);
            return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }
        return "";
    }

    private static string FullName(string ns, string name) =>
        ns.Length == 0 ? name : ns + "." + name;

    private static bool IsLegalXmlName(string name) =>
        name.Length is > 0 and <= 100 &&
        (char.IsLetter(name[0]) || name[0] == '_') &&
        name.Skip(1).All(c => char.IsLetterOrDigit(c) || c is '_' or '-');

    private sealed class MetadataTypeNames : ISignatureTypeProvider<string, object?>
    {
        public string GetArrayType(string type, ArrayShape shape) => type + "[]";
        public string GetByReferenceType(string type) => type + "&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "function*";
        public string GetGenericInstantiation(string genericType,
            ImmutableArray<string> typeArguments) =>
            genericType + "<" + string.Join(",", typeArguments) + ">";
        public string GetGenericMethodParameter(object? context, int index) => "!!" + index;
        public string GetGenericTypeParameter(object? context, int index) => "!" + index;
        public string GetModifiedType(string modifier, string unmodifiedType, bool required) =>
            unmodifiedType;
        public string GetPinnedType(string type) => type;
        public string GetPointerType(string type) => type + "*";
        public string GetPrimitiveType(PrimitiveTypeCode type) => type switch
        {
            PrimitiveTypeCode.Boolean => "System.Boolean",
            PrimitiveTypeCode.Byte => "System.Byte",
            PrimitiveTypeCode.SByte => "System.SByte",
            PrimitiveTypeCode.Int16 => "System.Int16",
            PrimitiveTypeCode.UInt16 => "System.UInt16",
            PrimitiveTypeCode.Int32 => "System.Int32",
            PrimitiveTypeCode.UInt32 => "System.UInt32",
            PrimitiveTypeCode.Int64 => "System.Int64",
            PrimitiveTypeCode.UInt64 => "System.UInt64",
            PrimitiveTypeCode.Single => "System.Single",
            PrimitiveTypeCode.Double => "System.Double",
            PrimitiveTypeCode.String => "System.String",
            PrimitiveTypeCode.Object => "System.Object",
            _ => type.ToString()
        };
        public string GetSZArrayType(string type) => type + "[]";
        public string GetTypeFromDefinition(MetadataReader reader,
            TypeDefinitionHandle handle, byte rawKind)
        {
            var type = reader.GetTypeDefinition(handle);
            return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }
        public string GetTypeFromReference(MetadataReader reader,
            TypeReferenceHandle handle, byte rawKind)
        {
            var type = reader.GetTypeReference(handle);
            return FullName(reader.GetString(type.Namespace), reader.GetString(type.Name));
        }
        public string GetTypeFromSpecification(MetadataReader reader, object? context,
            TypeSpecificationHandle handle, byte rawKind) =>
            reader.GetTypeSpecification(handle).DecodeSignature(this, context);
    }
}
