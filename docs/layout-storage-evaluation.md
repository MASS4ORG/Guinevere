# Layout storage and property blending evaluation

Issue #14 was evaluated against the real `LayoutNode` calculation path on .NET 10 in Release mode.
The benchmark project uses 100 warmups, then measures at least 100 iterations and one second per fixture.
The layout rows time layout only; text shaping is outside the timed region. A separate tree-build row reports node
construction cost. Allocation counts use `GC.GetAllocatedBytesForCurrentThread`.

Run it with:

```bash
dotnet run --project Examples/Example-90-LayoutBenchmarks/Example-90-LayoutBenchmarks.csproj -c Release
```

## Results

Measurements below were taken on 2026-09-19 in the repository development container. They are useful for
before/after comparison on this machine; compare fresh runs rather than treating the absolute values as portable.

| Scenario | Nodes | Before ms | After ms | After ns/node | Before allocation | After allocation |
|---|---:|---:|---:|---:|---:|---:|
| wide fixed | 1,001 | 0.0301 | 0.0163 | 16.28 | 40,120 B | 0 B |
| wide wrap | 1,001 | 0.0348 | 0.0113 | 11.27 | 45,416 B | 0 B |
| expand + min/max | 1,001 | 0.0253 | 0.0231 | 23.09 | 40,120 B | 0 B |
| fit nesting | 1,111 | 0.1318 | 0.0364 | 32.75 | 55,088 B | 0 B |
| measured text runs | 1,001 | 0.0349 | 0.0105 | 10.48 | 51,552 B | 0 B |

Wide fixtures remain approximately linear from 100 through 10,000 children. The 10,001-node results are 20.85
ns/node for fixed children, 12.34 ns/node for wrapping, and 24.73 ns/node for constrained expansion. Replacing the
heap `Rect` record with a 16-byte record struct removes the former 32-byte rectangle allocation per positioned node.
Pooled dimension buffers, direct aggregation loops, value-type contexts, and streaming wrap lines remove the other
per-layout allocations in the measured common cases.

Building the 10,001-node wide fixture allocates 5,454,664 bytes, or 545 bytes/node, including node IDs, child-list
growth, scopes, dictionaries, and draw lists. This retained-tree cost is now distinct from layout calculation, which
allocates zero bytes in the measured common cases.

Deep fit-content measurement now uses a bottom-up intrinsic-size cache for subtrees that do not depend on parent
constraints. Cumulative scroll offsets are propagated during the same traversal instead of walking every ancestor for
every positioned node. Retained trees skip layout entirely when clean; structural and fluent layout changes invalidate
ancestors automatically, while direct writes through the public `Style` field require `InvalidateLayout()`.
Depth scaling is linear after these changes: 65, 129, 257, 513, and 1,025-node chains measure approximately 57, 48,
48, 48, and 49 ns/node respectively, with zero allocations.

The suite includes every workload described in the PanGui article. Cases with directly corresponding Guinevere
semantics execute normally; `pixels_with_min_expand_constraint` remains in the output as explicitly unsupported until
min/max constraints accept composable expressions.

The full compatibility-guidance run on the development container produced:

| PanGui article fixture | Nodes | Guinevere ns/node | Allocation |
|---|---:|---:|---:|
| expand with max | 3,001 | 43.13 | 0 B |
| expand with min | 3,001 | 43.32 | 0 B |
| fit nesting | 101,111 | 88.19 | 0 B |
| equal expand weights | 15,001 | 34.21 | 0 B |
| unequal expand weights | 15,001 | 43.93 | 0 B |
| nested vertical stack | 10,001 | 30.69 | 0 B |
| padding and margin | 101 | 24.71 | 0 B |
| percentage and ratio | 10,001 | 65.34 | 0 B |
| perpendicular expand with wrap | 12,001 | 20.58 | 0 B |
| wide fixed | 100,001 | 79.75 | 0 B |
| wide wrapping | 10,001 | 22.13 | 0 B |
| pixels constrained by expand | — | unsupported | — |

These numbers are regression guidance, not a cross-machine leaderboard. PanGui's published results used different
hardware, runtime, and implementation details.

## Reusable acceptance criteria for layout issues

Add this paragraph to layout-related issues:

> **Performance acceptance:** Run
> `dotnet run --project Examples/Example-90-LayoutBenchmarks/Example-90-LayoutBenchmarks.csproj -c Release -- --quick`
> before and after the change on the same machine. The relevant fixture must remain correct, scale approximately
> linearly across its supplied sizes, introduce no unexpected steady-state allocations, and avoid a material timing
> regression. Include the before/after result in the issue or pull request. Add a focused fixture when the changed
> layout behavior is not represented; unsupported semantics must be reported explicitly rather than approximated
> silently.

## Property representation

`UnitValue` is now a value type containing six independent float coefficients: pixels, parent percentage,
perpendicular ratio, expand, fit-content, and fit-largest. Addition composes terms and `Lerp` interpolates every
coefficient. Consequently, animation between arbitrary modes has no tag switch and no discontinuity. The existing
float fluent overloads remain valid; `Width(UnitValue)`, `Height(UnitValue)`, and the `Gui.Node(UnitValue,
UnitValue)` overload opt into composition. The former implicit conversion from `UnitValue` back to a scalar is removed
because a blended expression has no truthful single scalar value; legacy `Mode` and `Value` accessors remain available.

The mutable object graph remains in place. A structure-of-arrays rewrite was rejected for this iteration because the
measured costs came from temporary collections and heap rectangles, while such a rewrite would affect node identity,
scope inheritance, interaction references, and the public fluent API. The targeted value storage change produces
zero steady-state allocations in representative fixtures without that migration cost.

## Correctness and limits

Differential fixtures retain the existing pixel, percentage, wrapping, constraints, and expansion behavior. New tests
cover coefficient composition, interpolation at five points, weighted expansion, fit-largest, and actual blended
layout resolution. Dirty reuse, fluent invalidation, and explicit invalidation have dedicated coverage. All 534 tests
pass.

`Rect` changing from a record class to a record struct is source-compatible for normal construction, property access,
operators, equality, and `with` expressions, but code relying on reference identity or null rectangles must migrate to
`Rect?`. This is the intentional breaking surface of the storage change.
