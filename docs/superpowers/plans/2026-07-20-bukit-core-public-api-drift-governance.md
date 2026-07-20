# Bukit Core Public API Drift Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a deterministic, classification-aware gate that rejects every unreviewed drift in the compiled public and protected CLR surface of the twelve Bukit Core assemblies.

**Architecture:** A standalone `net10.0` console tool owns canonical reflection capture, baseline validation, and comparison. Small shell entry points provide safe repository commands and mutation self-tests; `ci-fast` runs both the self-test and the real Release comparison. The checked baseline preserves G-01 ownership and compatibility decisions without making the dated G-01 report a runtime gate dependency.

**Tech Stack:** .NET 10, C# reflection, `System.Text.Json`, Bash, JSON Schema draft 2020-12, existing Bukit lightweight gates.

## Global Constraints

- Do not change any Core access modifier, namespace, type, member, assembly name, or project reference.
- Do not change `site.yaml`, `theme.yaml`, report, plugin, or persistence schemas.
- Do not change `bukit-plugin-v1`, asset URLs, runtime behavior, or AOT registration.
- Do not add a NuGet dependency or declare a supported CLR SDK.
- Every detected drift, including additive drift, must fail until an explicit baseline review accepts it.
- The snapshot path must be explicit, new, inside the repository or system temporary directory, and different from the governed baseline.
- Do not read from or modify `guide-0.1/`, `guide-0.2/`, `scripts-0.1/`, or `scripts-0.2/`.
- Do not run full, release, `test-all`, `smoke-all`, or whole-solution test gates.
- Use `bash scripts/checks/post-change-targeted.sh -- <changed paths>` after each code subtask.
- Because this changes a CI-owned gate, complete one independent bounded read-only audit after all targeted checks pass.

---

## File Map

### New tool files

- `tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj`: dependency-free `net10.0` executable definition.
- `tools/Bukit.PublicApiDrift/Program.cs`: command parsing, exit-code mapping, and bounded error reporting only.
- `tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs`: serialized baseline, assembly, type, and diagnostic records plus fixed policy vocabularies.
- `tools/Bukit.PublicApiDrift/BaselineFile.cs`: JSON loading, canonical serialization, committed/candidate validation, and safe writes.
- `tools/Bukit.PublicApiDrift/ApiSurfaceComparer.cs`: deterministic semantic comparison and diagnostic classification.
- `tools/Bukit.PublicApiDrift/ApiSignatureFormatter.cs`: assembly-independent CLR type/member signature formatting.
- `tools/Bukit.PublicApiDrift/ApiSurfaceCapture.cs`: exact assembly loading and exported-surface capture.

### New gate and fixture files

- `scripts/checks/public-api-drift.sh`: repository command wrapper for `check` and `snapshot`.
- `scripts/checks/public-api-drift-self-test.sh`: fixture mutations, exit-code assertions, and CI wiring assertions.
- `tests/fixtures/public-api-drift/*.json`: minimal canonical comparison inputs.
- `docs/governance/bukit-core-public-api-baseline.v1.json`: reviewed twelve-assembly baseline.
- `docs/schemas/bukit-core-public-api-baseline.v1.schema.json`: governance-artifact schema.

### Existing integration and documentation files

- `bukit-core.slnx`: include the standalone tool under `/tools/`.
- `scripts/gates/ci-fast.sh`: run self-test and real check exactly once in that order.
- `scripts/checks/docs/public-doc-contracts.sh`: require the guide, schema, and baseline.
- `docs/compatibility-governance.md`: English CLR visibility/support policy.
- `docs/compatibility-governance.zh-CN.md`: matching Chinese policy.
- `docs/bukit-1.0-contract-matrix.zh-CN.md`: replace the nonexistent source-generator SDK row.
- `guide/dev/public-api-governance.md`: maintainer update workflow and diagnostic meanings.
- `guide/dev/documentation-governance.md`: link the active CLR governance surface.

---

### Task 1: Comparator, canonical baseline validation, and mutation fixtures

**Files:**

- Create: `tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj`
- Create: `tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs`
- Create: `tools/Bukit.PublicApiDrift/BaselineFile.cs`
- Create: `tools/Bukit.PublicApiDrift/ApiSurfaceComparer.cs`
- Create: `tools/Bukit.PublicApiDrift/Program.cs`
- Create: `scripts/checks/public-api-drift-self-test.sh`
- Create: `tests/fixtures/public-api-drift/baseline.json`
- Create: `tests/fixtures/public-api-drift/unchanged.json`
- Create: `tests/fixtures/public-api-drift/additive.json`
- Create: `tests/fixtures/public-api-drift/removal.json`
- Create: `tests/fixtures/public-api-drift/protected-change.json`
- Create: `tests/fixtures/public-api-drift/stable-contract-change.json`
- Create: `tests/fixtures/public-api-drift/aot-change.json`
- Create: `tests/fixtures/public-api-drift/unclassified.json`
- Create: `tests/fixtures/public-api-drift/malformed.json`
- Create: `tests/fixtures/public-api-drift/unsorted.json`
- Create: `tests/fixtures/public-api-drift/unresolved-baseline.json`

**Interfaces:**

- Produces: `BaselineFile.Load(string, BaselineValidationMode)`, `BaselineFile.Serialize(ApiBaseline)`, `ApiSurfaceComparer.Compare(ApiBaseline, ApiBaseline)`, and CLI `compare BASELINE CURRENT`.
- Exit codes: `0` exact match, `1` valid drift, `2` invalid input or gate error.
- Diagnostics: one sorted line formatted as `<category>: <assembly>::<type>: <detail>`.

- [ ] **Step 1: Write the failing mutation self-test**

Create the executable script with a helper that distinguishes expected drift from tool failure:

```bash
#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
cd "$(repo_root)"

fail() { echo "public API drift self-test failed: $*" >&2; exit 1; }
tool=(dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj -c Release --no-restore -- compare)
fixtures="tests/fixtures/public-api-drift"

assert_exit() {
  local expected="$1" output="$2"; shift 2
  local status=0
  "$@" >"$output" 2>&1 || status=$?
  [[ "$status" == "$expected" ]] || fail "expected exit $expected, got $status: $(tr '\n' ' ' <"$output")"
}

scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-public-api-drift-self-test.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT

assert_exit 0 "$scratch/unchanged.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/unchanged.json"
assert_exit 1 "$scratch/additive.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/additive.json"
grep -Fq 'review-required:' "$scratch/additive.txt" || fail "additive drift lacks review-required"
if grep -Fq 'breaking:' "$scratch/additive.txt"; then fail "additive drift was mislabeled breaking"; fi
assert_exit 1 "$scratch/removal.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/removal.json"
grep -Fq 'breaking:' "$scratch/removal.txt" || fail "removal lacks breaking"
assert_exit 1 "$scratch/protected.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/protected-change.json"
grep -Fq 'protected-review:' "$scratch/protected.txt" || fail "protected drift lacks protected-review"
assert_exit 1 "$scratch/stable.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/stable-contract-change.json"
grep -Fq 'contract-shape-review:' "$scratch/stable.txt" || fail "stable contract drift lacks contract-shape-review"
assert_exit 1 "$scratch/aot.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/aot-change.json"
grep -Fq 'aot-review:' "$scratch/aot.txt" || fail "AOT drift lacks aot-review"
assert_exit 1 "$scratch/unclassified.txt" "${tool[@]}" "$fixtures/baseline.json" "$fixtures/unclassified.json"
grep -Fq 'unclassified:' "$scratch/unclassified.txt" || fail "new type lacks unclassified"
assert_exit 2 "$scratch/malformed.txt" "${tool[@]}" "$fixtures/malformed.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/malformed.txt" || fail "malformed baseline lacks gate-error"
assert_exit 2 "$scratch/unsorted.txt" "${tool[@]}" "$fixtures/unsorted.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/unsorted.txt" || fail "unsorted baseline lacks gate-error"
assert_exit 2 "$scratch/unresolved.txt" "${tool[@]}" "$fixtures/unresolved-baseline.json" "$fixtures/unchanged.json"
grep -Fq 'gate-error:' "$scratch/unresolved.txt" || fail "unresolved committed baseline lacks gate-error"

echo "public API drift self-test OK"
```

The canonical fixture root is:

```json
{
  "schema": "bukit-core-public-api-baseline-v1",
  "schemaVersion": 1,
  "targetFramework": "net10.0",
  "sdkPolicy": "no-general-clr-sdk",
  "assemblies": [
    {
      "assembly": "Fixture.Core",
      "project": "tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj"
    }
  ],
  "types": [
    {
      "assembly": "Fixture.Core",
      "name": "Fixture.Widget",
      "owner": "Fixture",
      "classification": "implementation-public",
      "compatibility": "2.0-candidate",
      "migrationHorizon": "2.0-review",
      "signature": "public sealed class Fixture.Widget",
      "publicMembers": ["public System.Void .ctor()", "public System.String Name { get; init; }"],
      "protectedMembers": []
    }
  ]
}
```

Create each mutation by copying that complete root and changing only its named condition: `additive.json` adds `public System.Int32 Count { get; }`; `removal.json` removes `Name`; `protected-change.json` adds `protected System.Void Reset()`; `stable-contract-change.json` changes classification to `serialized-contract` and changes `Name` to `Title`; `aot-change.json` changes classification to `aot-serialization-surface` and adds `public System.Int32 Version { get; }`; `unclassified.json` adds `Fixture.NewWidget` with all three policy fields set to `review-required`; `malformed.json` duplicates `Name` in `publicMembers`; `unsorted.json` reverses the two baseline public members; `unresolved-baseline.json` sets the committed entry owner to `unresolved-owner-review`. `unchanged.json` is byte-identical to `baseline.json`.

- [ ] **Step 2: Run the self-test and verify the red state**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
```

Expected: non-zero before fixture comparison because `tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj` does not yet exist.

- [ ] **Step 3: Add the tool project and exact serialized model**

Use this project definition:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <AssemblyName>Bukit.PublicApiDrift</AssemblyName>
    <RootNamespace>Bukit.PublicApiDrift</RootNamespace>
  </PropertyGroup>
</Project>
```

Define these exact immutable records and vocabularies in `ApiSurfaceModels.cs`:

```csharp
namespace Bukit.PublicApiDrift;

internal sealed record ApiBaseline(
    string Schema,
    int SchemaVersion,
    string TargetFramework,
    string SdkPolicy,
    IReadOnlyList<ApiAssembly> Assemblies,
    IReadOnlyList<ApiType> Types);

internal sealed record ApiAssembly(string Assembly, string Project);

internal sealed record ApiType(
    string Assembly,
    string Name,
    string Owner,
    string Classification,
    string Compatibility,
    string MigrationHorizon,
    string Signature,
    IReadOnlyList<string> PublicMembers,
    IReadOnlyList<string> ProtectedMembers);

internal sealed record DriftDiagnostic(string Category, string Assembly, string TypeName, string Detail)
{
    public override string ToString() => $"{Category}: {Assembly}::{TypeName}: {Detail}";
}

internal enum BaselineValidationMode { Committed, Candidate }

internal static class ApiPolicy
{
    public const string Schema = "bukit-core-public-api-baseline-v1";
    public static readonly HashSet<string> Classifications = new(StringComparer.Ordinal)
    {
        "aot-serialization-surface", "cross-assembly-implementation", "implementation-public",
        "persisted-internal-format", "plugin-wire-contract", "serialized-contract"
    };
    public static readonly HashSet<string> Compatibility = new(StringComparer.Ordinal)
    {
        "1.x-do-not-narrow", "1.x-migration-safe", "1.x-shape-stable",
        "2.0-candidate", "not-a-clr-contract"
    };
}
```

- [ ] **Step 4: Implement canonical loading and strict validation**

`BaselineFile.Load` must deserialize with case-sensitive property names, reject trailing tokens/comments, validate fixed root values, sort order, duplicates, policy values, missing project paths for committed baselines, and unresolved candidate metadata. Its public-internal surface is:

```csharp
internal static class BaselineFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        WriteIndented = true
    };

    public static ApiBaseline Load(string path, BaselineValidationMode mode);
    public static string Serialize(ApiBaseline baseline);
    public static void WriteNew(string path, ApiBaseline baseline, string repositoryRoot);
    private static void Validate(ApiBaseline baseline, BaselineValidationMode mode);
}
```

`Serialize` must return UTF-8-compatible text with LF line endings and one final LF. `Load` in `Committed` mode must compare `Serialize(result)` byte-for-byte with normalized input and reject non-canonical formatting. `WriteNew` must use `FileMode.CreateNew` and must not follow an existing destination.

- [ ] **Step 5: Implement classification-aware comparison**

Use exact tuple keys and sorted output:

```csharp
internal static class ApiSurfaceComparer
{
    public static IReadOnlyList<DriftDiagnostic> Compare(ApiBaseline baseline, ApiBaseline current)
    {
        var diagnostics = new List<DriftDiagnostic>();
        var oldTypes = baseline.Types.ToDictionary(TypeKey, StringComparer.Ordinal);
        var newTypes = current.Types.ToDictionary(TypeKey, StringComparer.Ordinal);

        foreach (var key in oldTypes.Keys.Except(newTypes.Keys, StringComparer.Ordinal))
            Add(diagnostics, oldTypes[key], "breaking", "exported type removed");

        foreach (var key in newTypes.Keys.Except(oldTypes.Keys, StringComparer.Ordinal))
        {
            var type = newTypes[key];
            Add(diagnostics, type, "review-required", "exported type added");
            if (!ApiPolicy.Classifications.Contains(type.Classification))
                Add(diagnostics, type, "unclassified", "new type requires approved classification");
        }

        foreach (var key in oldTypes.Keys.Intersect(newTypes.Keys, StringComparer.Ordinal))
            CompareType(oldTypes[key], newTypes[key], diagnostics);

        return diagnostics.OrderBy(static item => item.Category, StringComparer.Ordinal)
            .ThenBy(static item => item.Assembly, StringComparer.Ordinal)
            .ThenBy(static item => item.TypeName, StringComparer.Ordinal)
            .ThenBy(static item => item.Detail, StringComparer.Ordinal)
            .ToArray();
    }

    private static string TypeKey(ApiType type) => $"{type.Assembly}\u0000{type.Name}";
    private static void Add(List<DriftDiagnostic> items, ApiType type, string category, string detail) =>
        items.Add(new(category, type.Assembly, type.Name, detail));
}
```

`CompareType` must compare metadata, type signature, public members, and protected members independently. Removed public members emit `breaking`; added public members emit `review-required`; every protected delta emits `protected-review`; a signature delta emits `type-shape-review`. Any delta on either side classified `plugin-wire-contract` or `serialized-contract` additionally emits one deduplicated `contract-shape-review`; any delta on either side classified `aot-serialization-surface` additionally emits one deduplicated `aot-review`. Classification, compatibility, owner, or migration-horizon changes emit `review-required` with old and new values.

- [ ] **Step 6: Implement only the `compare` command path**

`Program.cs` must keep exception formatting bounded and map errors to exit 2:

```csharp
namespace Bukit.PublicApiDrift;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["compare", var baselinePath, var currentPath])
            {
                var baseline = BaselineFile.Load(baselinePath, BaselineValidationMode.Committed);
                var current = BaselineFile.Load(currentPath, BaselineValidationMode.Candidate);
                var diagnostics = ApiSurfaceComparer.Compare(baseline, current);
                foreach (var item in diagnostics) Console.Error.WriteLine(item);
                return diagnostics.Count == 0 ? 0 : 1;
            }

            Console.Error.WriteLine("usage: Bukit.PublicApiDrift compare BASELINE CURRENT | check BASELINE ROOT CONFIGURATION | snapshot BASELINE OUTPUT ROOT CONFIGURATION");
            return 2;
        }
        catch (Exception exception)
        {
            var root = exception.GetBaseException();
            var message = root.Message.Replace('\r', ' ').Replace('\n', ' ');
            if (message.Length > 400) message = message[..400];
            Console.Error.WriteLine($"gate-error: {root.GetType().FullName}: {message}");
            return 2;
        }
    }
}
```

- [ ] **Step 7: Run the mutation self-test and targeted gate**

Run:

```bash
dotnet restore tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/post-change-targeted.sh -- \
  tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  tools/Bukit.PublicApiDrift/ApiSurfaceModels.cs \
  tools/Bukit.PublicApiDrift/BaselineFile.cs \
  tools/Bukit.PublicApiDrift/ApiSurfaceComparer.cs \
  tools/Bukit.PublicApiDrift/Program.cs \
  scripts/checks/public-api-drift-self-test.sh \
  tests/fixtures/public-api-drift
```

Expected: self-test prints `public API drift self-test OK`; targeted gate passes. If `post-change-targeted` reaches `ci-fast`, the new check is not wired yet, so existing `ci-fast` remains green.

- [ ] **Step 8: Commit Task 1**

```bash
git add tools/Bukit.PublicApiDrift scripts/checks/public-api-drift-self-test.sh tests/fixtures/public-api-drift
git commit -m "test(governance): define public API drift policy"
```

---

### Task 2: Reflection capture, signature formatting, and reviewed Core baseline

**Files:**

- Create: `tools/Bukit.PublicApiDrift/ApiSignatureFormatter.cs`
- Create: `tools/Bukit.PublicApiDrift/ApiSurfaceCapture.cs`
- Create: `docs/schemas/bukit-core-public-api-baseline.v1.schema.json`
- Create: `docs/governance/bukit-core-public-api-baseline.v1.json`
- Modify: `tools/Bukit.PublicApiDrift/Program.cs`

**Interfaces:**

- Consumes: Task 1 models, canonical serialization, and comparer.
- Produces: `ApiSurfaceCapture.Capture(ApiBaseline, string, string)` and CLI `check BASELINE ROOT CONFIGURATION` / `snapshot BASELINE OUTPUT ROOT CONFIGURATION`.
- The governed baseline contains exactly 12 assembly mappings and 472 resolved exported type entries at the G-01 baseline commit.

- [ ] **Step 1: Add failing capture and determinism assertions to the self-test**

Append checks that require the real baseline and capture implementation:

```bash
baseline="docs/governance/bukit-core-public-api-baseline.v1.json"
assert_exit 0 "$scratch/real-check.txt" dotnet run \
  --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-restore -- check "$baseline" "$PWD" Release

first="$scratch/first.json"
second="$scratch/second.json"
assert_exit 0 "$scratch/snapshot-1.txt" dotnet run \
  --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-restore -- snapshot "$baseline" "$first" "$PWD" Release
assert_exit 0 "$scratch/snapshot-2.txt" dotnet run \
  --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj \
  -c Release --no-restore -- snapshot "$baseline" "$second" "$PWD" Release
cmp -s "$first" "$second" || fail "two captures are not byte-identical"
[[ "$(jq '.assemblies | length' "$first")" == "12" ]] || fail "capture does not contain 12 assemblies"
[[ "$(jq '.types | length' "$first")" == "472" ]] || fail "capture does not contain 472 exported types"
```

- [ ] **Step 2: Run the self-test and verify capture is red**

Run `bash scripts/checks/public-api-drift-self-test.sh`.

Expected: exit 2 with the usage line because `check` and `snapshot` are not implemented.

- [ ] **Step 3: Implement canonical CLR formatting**

Define this focused formatter surface:

```csharp
internal static class ApiSignatureFormatter
{
    public static string FormatType(Type type);
    public static IReadOnlyList<string> FormatPublicMembers(Type type);
    public static IReadOnlyList<string> FormatProtectedMembers(Type type);
    private static string FormatMethod(MethodBase method, NullabilityInfoContext nullability);
    private static string FormatProperty(PropertyInfo property, NullabilityInfoContext nullability);
    private static string FormatField(FieldInfo field, NullabilityInfoContext nullability);
    private static string FormatEvent(EventInfo @event, NullabilityInfoContext nullability);
    private static string FormatTypeName(Type type, NullabilityInfo? nullability = null);
    private static string FormatGenericConstraints(Type parameter);
    private static string FormatDefault(object? value);
}
```

Use `BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic`. Include public constructors/methods/properties/fields/events in `PublicMembers`; include `Family` and `FamilyOrAssembly` accessors/members in `ProtectedMembers`; exclude `FamilyAndAssembly`; exclude special-name property/event accessors to prevent duplicates. Sort with `StringComparer.Ordinal`. Type names must use namespace-qualified CLR names, generic arguments, `[]`, pointer, nullable annotation (`?`, `!`, or `~` for unknown), and `ref`/`in`/`out`. Default values use `CultureInfo.InvariantCulture`, quoted escaped strings/chars, `null`, and fully qualified enum names.

- [ ] **Step 4: Implement exact assembly capture**

Use an unloadable `AssemblyLoadContext` rooted at each assembly directory and resolve dependencies from the same directory before falling back to the default context. The capture surface is:

```csharp
internal static class ApiSurfaceCapture
{
    public static ApiBaseline Capture(ApiBaseline policy, string repositoryRoot, string configuration)
    {
        var types = new List<ApiType>();
        foreach (var mapping in policy.Assemblies.OrderBy(static item => item.Assembly, StringComparer.Ordinal))
        {
            var dll = Path.Combine(repositoryRoot, Path.GetDirectoryName(mapping.Project)!, "bin", configuration,
                policy.TargetFramework, mapping.Assembly + ".dll");
            if (!File.Exists(dll)) throw new FileNotFoundException("compiled assembly is missing", dll);
            var context = new ApiAssemblyLoadContext(Path.GetDirectoryName(dll)!);
            try
            {
                var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(dll));
                if (!StringComparer.Ordinal.Equals(assembly.GetName().Name, mapping.Assembly))
                    throw new InvalidDataException($"unexpected assembly name for {mapping.Project}");
                foreach (var type in assembly.GetExportedTypes().OrderBy(static item => item.FullName, StringComparer.Ordinal))
                    types.Add(CaptureType(mapping.Assembly, type, policy));
            }
            finally
            {
                context.Unload();
            }
        }
        return policy with { Types = types.OrderBy(static item => item.Assembly, StringComparer.Ordinal)
            .ThenBy(static item => item.Name, StringComparer.Ordinal).ToArray() };
    }
}

internal sealed class ApiAssemblyLoadContext(string assemblyDirectory) : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var candidate = Path.Combine(assemblyDirectory, assemblyName.Name + ".dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
```

`CaptureType` must preserve owner/classification/compatibility/migration values from the matching baseline type. An unknown type receives `owner = "unresolved-owner-review"`, `classification = "review-required"`, `compatibility = "review-required"`, and `migrationHorizon = "review-required"`. Do not serialize DLL paths, MVIDs, timestamps, source paths, or load-context details.

- [ ] **Step 5: Implement `check` and `snapshot` command branches**

Add exact argument branches before the usage error:

```csharp
if (args is ["check", var baselinePath, var repositoryRoot, var configuration])
{
    var baseline = BaselineFile.Load(baselinePath, BaselineValidationMode.Committed);
    var current = ApiSurfaceCapture.Capture(baseline, repositoryRoot, configuration);
    var diagnostics = ApiSurfaceComparer.Compare(baseline, current);
    foreach (var item in diagnostics) Console.Error.WriteLine(item);
    return diagnostics.Count == 0 ? 0 : 1;
}

if (args is ["snapshot", var baselinePath, var outputPath, var repositoryRoot, var configuration])
{
    var baseline = BaselineFile.Load(baselinePath, BaselineValidationMode.Committed);
    var current = ApiSurfaceCapture.Capture(baseline, repositoryRoot, configuration);
    BaselineFile.WriteNew(outputPath, current, repositoryRoot);
    Console.Out.WriteLine($"wrote public API candidate: {Path.GetFullPath(outputPath)}");
    return 0;
}
```

Do not build or restore inside the .NET process; the shell wrapper in Task 3 owns build orchestration.

- [ ] **Step 6: Add the v1 governance schema**

The schema must set `additionalProperties: false` at the root and every object, require every serialized property, enumerate the six classifications and five compatibility values, require unique arrays, and constrain `schema` to `bukit-core-public-api-baseline-v1`, `schemaVersion` to `1`, `targetFramework` to `net10.0`, and `sdkPolicy` to `no-general-clr-sdk`.

- [ ] **Step 7: Bootstrap and review the initial baseline mechanically**

Build all Core assemblies and the tool without changing Core source:

```bash
dotnet restore bukit-core.slnx
dotnet build bukit-core.slnx -c Release --no-restore
```

Generate a temporary policy seed by mapping every G-01 inventory `types[]` entry to `assembly`, `fullName -> name`, `owner`, `classification`, `compatibility`, and `migrationHorizon`; use the exact twelve project mappings from `bukit-core.slnx`. Run the capture into a new candidate, then promote the generated candidate as the governed baseline only after these assertions pass:

```bash
jq -e '.schema == "bukit-core-public-api-baseline-v1" and .schemaVersion == 1' docs/governance/bukit-core-public-api-baseline.v1.json
jq -e '(.assemblies | length) == 12 and (.types | length) == 472' docs/governance/bukit-core-public-api-baseline.v1.json
jq -e '[.types[] | select(.owner == "unresolved-owner-review" or .classification == "review-required" or .compatibility == "review-required" or .migrationHorizon == "review-required")] | length == 0' docs/governance/bukit-core-public-api-baseline.v1.json
jq -e '[.types[].classification] | group_by(.) | map({(.[0]): length}) | add == {"aot-serialization-surface":3,"cross-assembly-implementation":170,"implementation-public":182,"persisted-internal-format":6,"plugin-wire-contract":23,"serialized-contract":88}' docs/governance/bukit-core-public-api-baseline.v1.json
```

The bootstrap transformation is implementation-only and must not remain as an active script or runtime dependency. The committed checker reads only the governed baseline.

- [ ] **Step 8: Run determinism and targeted verification**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/post-change-targeted.sh -- \
  tools/Bukit.PublicApiDrift/ApiSignatureFormatter.cs \
  tools/Bukit.PublicApiDrift/ApiSurfaceCapture.cs \
  tools/Bukit.PublicApiDrift/Program.cs \
  scripts/checks/public-api-drift-self-test.sh \
  docs/schemas/bukit-core-public-api-baseline.v1.schema.json \
  docs/governance/bukit-core-public-api-baseline.v1.json
```

Expected: self-test reports exact 12/472 capture, two snapshots compare byte-identically, the real comparison exits 0, and targeted gate passes.

- [ ] **Step 9: Commit Task 2**

```bash
git add tools/Bukit.PublicApiDrift docs/schemas/bukit-core-public-api-baseline.v1.schema.json docs/governance/bukit-core-public-api-baseline.v1.json scripts/checks/public-api-drift-self-test.sh
git commit -m "feat(governance): capture Bukit Core public API baseline"
```

---

### Task 3: Safe repository command and CI gate integration

**Files:**

- Create: `scripts/checks/public-api-drift.sh`
- Modify: `scripts/checks/public-api-drift-self-test.sh`
- Modify: `bukit-core.slnx`
- Modify: `scripts/gates/ci-fast.sh`
- Modify: `scripts/checks/docs/public-doc-contracts.sh`

**Interfaces:**

- Consumes: Task 2 tool commands and governed baseline.
- Produces: `bash scripts/checks/public-api-drift.sh check [Configuration]` and `bash scripts/checks/public-api-drift.sh snapshot OUTPUT [Configuration]`.
- Makes both self-test and real drift check mandatory in `ci-fast` and therefore release qualification.

- [ ] **Step 1: Extend the self-test with failing wrapper and wiring checks**

Append:

```bash
expected_self_test='run_step "public API drift self-test" bash scripts/checks/public-api-drift-self-test.sh'
expected_real_gate='run_step "public API drift" bash scripts/checks/public-api-drift.sh check "$configuration"'
[[ "$(grep -Fxc "$expected_self_test" scripts/gates/ci-fast.sh)" == "1" ]] || fail "ci-fast self-test wiring is missing or duplicated"
[[ "$(grep -Fxc "$expected_real_gate" scripts/gates/ci-fast.sh)" == "1" ]] || fail "ci-fast real-check wiring is missing or duplicated"

assert_exit 2 "$scratch/missing-output.txt" bash scripts/checks/public-api-drift.sh snapshot
assert_exit 2 "$scratch/baseline-overwrite.txt" bash scripts/checks/public-api-drift.sh snapshot docs/governance/bukit-core-public-api-baseline.v1.json Release
touch "$scratch/existing.json"
assert_exit 2 "$scratch/existing-output.txt" bash scripts/checks/public-api-drift.sh snapshot "$scratch/existing.json" Release
outside="$(dirname "$PWD")/bukit-public-api-outside-$$.json"
assert_exit 2 "$scratch/outside-output.txt" bash scripts/checks/public-api-drift.sh snapshot "$outside" Release
assert_exit 0 "$scratch/wrapper-check.txt" bash scripts/checks/public-api-drift.sh check Release
```

- [ ] **Step 2: Run the self-test and verify missing integration fails**

Run `bash scripts/checks/public-api-drift-self-test.sh`.

Expected: failure stating the `ci-fast` self-test wiring is missing.

- [ ] **Step 3: Implement the safe shell wrapper**

Create:

```bash
#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/common.sh"
root="$(repo_root)"
cd "$root"

usage() { echo "usage: bash scripts/checks/public-api-drift.sh <check [Configuration]|snapshot OUTPUT [Configuration]>" >&2; }
[[ $# -ge 1 ]] || { usage; exit 2; }

mode="$1"; shift
baseline="docs/governance/bukit-core-public-api-baseline.v1.json"
case "$mode" in
  check)
    [[ $# -le 1 ]] || { usage; exit 2; }
    configuration="${1:-Release}"
    ;;
  snapshot)
    [[ $# -ge 1 && $# -le 2 && -n "$1" ]] || { usage; exit 2; }
    output="$1"; configuration="${2:-Release}"
    ;;
  *) usage; exit 2 ;;
esac

dotnet build bukit-core.slnx -c "$configuration" --no-restore --nologo >&2

tool=(dotnet run --project tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj -c "$configuration" --no-build --no-restore --)
if [[ "$mode" == "check" ]]; then
  "${tool[@]}" check "$baseline" "$root" "$configuration"
else
  "${tool[@]}" snapshot "$baseline" "$output" "$root" "$configuration"
fi
```

Path safety is enforced again by `BaselineFile.WriteNew` after canonical `Path.GetFullPath` normalization. Accept only a descendant of repository root or the canonical `${TMPDIR:-/tmp}` root; reject equality with the governed baseline, an existing file/directory, and symlink/reparse destinations.

- [ ] **Step 4: Add the tool to the Core solution**

Add this sibling folder after the Core folder without altering any Core project reference:

```xml
  <Folder Name="/tools/">
    <Project Path="tools/Bukit.PublicApiDrift/Bukit.PublicApiDrift.csproj" />
  </Folder>
```

- [ ] **Step 5: Wire self-test and real check into `ci-fast`**

Add after `post-change targeted self-test` and before later contract checks:

```bash
run_step "public API drift self-test" bash scripts/checks/public-api-drift-self-test.sh
run_step "public API drift" bash scripts/checks/public-api-drift.sh check "$configuration"
```

If `ci-fast.sh` does not currently define `configuration`, add immediately after `cd "$(repo_root)"`:

```bash
configuration="${1:-Release}"
[[ $# -le 1 ]] || { echo "usage: bash scripts/gates/ci-fast.sh [Configuration]" >&2; exit 2; }
```

- [ ] **Step 6: Promote the three governance paths into public-doc contracts**

Add exactly these entries to `required=(...)`:

```bash
  guide/dev/public-api-governance.md
  docs/governance/bukit-core-public-api-baseline.v1.json
  docs/schemas/bukit-core-public-api-baseline.v1.schema.json
```

- [ ] **Step 7: Run direct and owner gates**

Run:

```bash
bash -n scripts/checks/public-api-drift.sh
bash -n scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
bash scripts/checks/post-change-targeted.sh -- \
  scripts/checks/public-api-drift.sh \
  scripts/checks/public-api-drift-self-test.sh \
  bukit-core.slnx \
  scripts/gates/ci-fast.sh \
  scripts/checks/docs/public-doc-contracts.sh
```

Expected: wrapper safety mutations pass, direct real check exits 0, and the owner `ci-fast` gate passes with both new step labels exactly once.

- [ ] **Step 8: Commit Task 3**

```bash
git add scripts/checks/public-api-drift.sh scripts/checks/public-api-drift-self-test.sh bukit-core.slnx scripts/gates/ci-fast.sh scripts/checks/docs/public-doc-contracts.sh
git commit -m "ci(governance): enforce public API drift review"
```

---

### Task 4: Active governance documentation and stale contract correction

**Files:**

- Create: `guide/dev/public-api-governance.md`
- Modify: `guide/dev/documentation-governance.md`
- Modify: `docs/compatibility-governance.md`
- Modify: `docs/compatibility-governance.zh-CN.md`
- Modify: `docs/bukit-1.0-contract-matrix.zh-CN.md`

**Interfaces:**

- Consumes: the exact commands, diagnostics, and baseline from Tasks 1–3.
- Produces: active maintainer policy; no product/runtime contract changes.

- [ ] **Step 1: Add a failing documentation-contract assertion**

In `public-api-drift-self-test.sh`, assert exact active wording and removal of the stale claim:

```bash
grep -Fq 'C# `public` is CLR visibility, not an automatic supported SDK promise.' guide/dev/public-api-governance.md || fail "CLR visibility policy is missing"
grep -Fq 'bash scripts/checks/public-api-drift.sh snapshot OUTPUT Release' guide/dev/public-api-governance.md || fail "snapshot workflow is missing"
if grep -Fq 'Source-generated plugin SDK' docs/bukit-1.0-contract-matrix.zh-CN.md; then fail "stale source-generated SDK claim remains"; fi
grep -Fq 'Process protocol DTO and static JSON serialization support' docs/bukit-1.0-contract-matrix.zh-CN.md || fail "implemented plugin boundary is missing"
```

- [ ] **Step 2: Run self-test and verify documentation is red**

Run `bash scripts/checks/public-api-drift-self-test.sh`.

Expected: failure stating `CLR visibility policy is missing`.

- [ ] **Step 3: Write the maintainer governance page**

The page must include these sections and exact rules:

```markdown
# Public API Governance

C# `public` is CLR visibility, not an automatic supported SDK promise.

Bukit's supported external surfaces are CLI behavior, configuration and theme
shapes, template objects, report schemas, and the `bukit-plugin-v1` process
protocol. Bukit does not currently distribute a general-purpose Core CLR SDK.

## Check

`bash scripts/checks/public-api-drift.sh check Release`

## Review a Legitimate Change

1. Run `bash scripts/checks/public-api-drift.sh snapshot OUTPUT Release`.
2. Review every type/member diff and assign owner, classification,
   compatibility, migration horizon, and reason.
3. Run the relevant schema, protocol, or AOT contract tests.
4. Replace the governed baseline only in the reviewed change.
5. Run the self-test, real check, `ci-fast`, and Architecture tests.

Never infer removal safety from zero repository-local consumers. Access
narrowing remains a separate major-version task.
```

Also document all diagnostic categories and exit codes from the approved design, the explicit-output/no-overwrite snapshot safety boundary, and the six classification plus five compatibility values.

- [ ] **Step 4: Update active compatibility governance in both languages**

Add a new `CG-021` row in both files. English must state `CLR public visibility is not a general SDK support promise`; Chinese must state `CLR public 可见性不等于通用 SDK 支持承诺`. Both rows link the new guide and baseline, use status `supported-by-policy`, and make baseline review mandatory before any public/protected drift is accepted.

- [ ] **Step 5: Correct the Chinese 1.0 contract matrix**

Replace only the stale row with:

```markdown
| Process protocol DTO and static JSON serialization support | `GA-limited` | `bukit-plugin-v1` JSON shape and AOT serializer context are governed; third-party process plugins do not reference Bukit CLR assemblies | protocol/schema drift rejected | Plugin protocol + AOT serialization tests |
```

Do not edit historical plan copies of the old row and do not change the protocol itself.

- [ ] **Step 6: Link the new active surface from documentation governance**

Under `Checked Surfaces`, add:

```markdown
- CLR public/protected surface baseline and maintainer workflow under
  `docs/governance/bukit-core-public-api-baseline.v1.json` and
  `guide/dev/public-api-governance.md`.
```

- [ ] **Step 7: Run documentation and targeted gates**

Run:

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/docs-consistency.sh
bash scripts/checks/post-change-targeted.sh -- \
  guide/dev/public-api-governance.md \
  guide/dev/documentation-governance.md \
  docs/compatibility-governance.md \
  docs/compatibility-governance.zh-CN.md \
  docs/bukit-1.0-contract-matrix.zh-CN.md \
  scripts/checks/public-api-drift-self-test.sh
```

Expected: no stale `Source-generated plugin SDK` claim in the active matrix, language policies agree, docs consistency passes, and real API drift remains clean.

- [ ] **Step 8: Commit Task 4**

```bash
git add guide/dev/public-api-governance.md guide/dev/documentation-governance.md docs/compatibility-governance.md docs/compatibility-governance.zh-CN.md docs/bukit-1.0-contract-matrix.zh-CN.md scripts/checks/public-api-drift-self-test.sh
git commit -m "docs(governance): define CLR public surface policy"
```

---

### Task 5: Consolidated verification, independent audit, and delivery evidence

**Files:**

- Review only: every path changed by Tasks 1–4
- Modify only if a failed check or audit identifies an in-scope defect

**Interfaces:**

- Consumes: the completed aggregate G-02 diff.
- Produces: reproducible proof that G-02 detects drift without changing Core behavior or contracts.

- [ ] **Step 1: Run direct governance checks**

```bash
bash scripts/checks/public-api-drift-self-test.sh
bash scripts/checks/public-api-drift.sh check Release
```

Expected: `public API drift self-test OK`; real check exits 0 with no drift diagnostics.

- [ ] **Step 2: Run the gate owner and Architecture tests**

```bash
bash scripts/gates/ci-fast.sh Release
dotnet test tests/Bukit.Architecture.Tests/Bukit.Architecture.Tests.csproj -c Release --no-restore
```

Expected: `ci-fast` passes and Architecture tests report all tests passed. Do not suppress NuGet audit failures unless the repository's current approved command explicitly requires it; classify an environmental restore failure separately from test results.

- [ ] **Step 3: Prove deterministic capture and no hidden machine data**

```bash
scratch="$(mktemp -d "${TMPDIR:-/tmp}/bukit-public-api-final.XXXXXX")"
trap 'rm -rf -- "$scratch"' EXIT
bash scripts/checks/public-api-drift.sh snapshot "$scratch/one.json" Release
bash scripts/checks/public-api-drift.sh snapshot "$scratch/two.json" Release
cmp "$scratch/one.json" "$scratch/two.json"
if rg -n '/Users/|[A-Za-z]:\\|MVID|timestamp|bin/Release' "$scratch/one.json"; then exit 1; fi
```

Expected: byte-identical candidates and no local absolute path/build identity leakage.

- [ ] **Step 4: Run repository hygiene checks**

```bash
git diff --check main...HEAD
rg -n 'TO[D]O|T[B]D|FIX[M]E|PLACEH[O]LDER|待[补]|待[定]' \
  tools/Bukit.PublicApiDrift scripts/checks/public-api-drift.sh \
  scripts/checks/public-api-drift-self-test.sh guide/dev/public-api-governance.md \
  docs/governance docs/schemas/bukit-core-public-api-baseline.v1.schema.json && exit 1 || true
git diff --name-only main...HEAD -- src/Bukit-Core guide-0.1 guide-0.2 scripts-0.1 scripts-0.2
```

Expected: whitespace check passes, placeholder scan is empty, and the protected path diff is empty.

- [ ] **Step 5: Request the required independent read-only audit**

The reviewer receives `main...HEAD`, the approved design, this implementation plan, direct gate outputs, and these review questions:

```text
1. Does every task remain inside G-02 public-surface governance scope?
2. Can additive, breaking, protected, stable-contract, AOT, unclassified, or invalid-input drift pass silently?
3. Can snapshot overwrite the governed baseline, an existing path, or escape repository/tmp boundaries?
4. Is capture deterministic across supported hosts and free from machine-local data?
5. Did any Core API, runtime behavior, schema, protocol, persistence format, or backup area change?
6. Are ci-fast self-test and real check both mandatory exactly once?
```

The reviewer must not modify files or create commits. Any important finding stops delivery; fix only the affected G-02 scope, rerun its targeted gate, then repeat the necessary audit.

- [ ] **Step 6: Record final status and commit audit-driven corrections if any**

If no correction is needed, do not create an empty commit. If an in-scope correction was required:

```bash
git add tools/Bukit.PublicApiDrift scripts/checks/public-api-drift.sh \
  scripts/checks/public-api-drift-self-test.sh bukit-core.slnx \
  scripts/gates/ci-fast.sh scripts/checks/docs/public-doc-contracts.sh \
  docs/governance/bukit-core-public-api-baseline.v1.json \
  docs/schemas/bukit-core-public-api-baseline.v1.schema.json \
  guide/dev/public-api-governance.md guide/dev/documentation-governance.md \
  docs/compatibility-governance.md docs/compatibility-governance.zh-CN.md \
  docs/bukit-1.0-contract-matrix.zh-CN.md
git commit -m "fix(governance): address G-02 review findings"
```

Rerun Steps 1–4 after any correction. Report implemented, verified, environment-blocked, and still-open evidence separately.

- [ ] **Step 7: Present branch completion options**

Use `superpowers:finishing-a-development-branch` only after all required checks and the read-only audit have no unresolved important issue. Do not merge, push, or open a pull request without the user's explicit choice.
