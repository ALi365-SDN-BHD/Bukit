# Design

## Approved ownership

Move the current canonical sources, not old migration blobs, into
`src/Bukit-Core/Bukit.Content/Notion/`. Keep namespace `Bukit.Content.Notion`.
The public source uses its existing LoadRawAsync implementation for
IContentProvider and explicitly forwards the internal relation-fallback provider
to CreateRelationFallbackResolver. Composite therefore retains the actual
source-bounded fallback after all sources load. The fallback property stays
non-public. The existing public constructor keeps nullable ILogger default null.

NotionContentClient retains its transport implementation and gains only the
internal handler/delay/clock constructor seam needed by retained deterministic
retry tests; the three-argument handler overload chains to it. Engine calls
canonical query/option helpers through the same Transport overloads. The page
fetcher's null-client guard stays inside its existing try before Transport access,
preserving the current ArgumentNullException warning payload and caller logger.

## Invariants

HTTP ownership, fail-closed injected clients, SSRF/auth/secret handling, replay
semantics, errors and cancellation MUST remain unchanged. Cache paths, versions,
atomicity and readonly behavior, raw/body/field/date contracts, schema/query
construction, relation/taxonomy projection and ordering MUST remain unchanged.
The inherited real HTTP 403/404 fallback classification remains unresolved;
repair requires a separately confirmed Core task, not this migration.

Only obsolete runtime project references/friend edges are removed; Abstractions
grants Content the access needed by the moved projector. Existing test project
identities/friends remain. No Engine/module friend or public helper is added.

## CLR migration and evidence

The parser and its two public methods are deleted without an equivalent public
parser. LoadRawAsync is a content-loading workflow, not a mechanical parser
replacement. Source/options keep their full names but change assembly identity;
old binaries, reflection/serializer bindings and assembly-qualified names require
explicit migration and recompilation. No type forwarder retains the old DLL.

The independent before/expected API snapshots constrain all 423 other entries,
moved metadata, source constructor and options' 31 public members. Only the
source's required two interface entries and compiler-derived virtual-final flag
on the existing LoadRawAsync row accompany the owner move. Frozen historical
manifests and AD-03C5's original byte prefix remain intact; a dated section 8
records the separately approved reversal.

Use the finalized external complete-class baseline/RED/build/GREEN commands,
serial resource classification, exact owner regressions and per-specialty cache
inputs. No whole-solution or aggregate gate is inferred. Runtime characterization
uses real source-through-Composite behavior with fake HTTP on both sides;
structural RED is solely the Architecture migration class. Final proof and review
outcomes stay in external task evidence, not a second repository checklist.
