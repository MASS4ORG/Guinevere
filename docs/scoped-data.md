# Scoped GUI data

`Gui` owns persistent data independently of the layout tree. `GetData`, `SetData` and `UpdateData` use the same numeric identity registry as nodes, controls and GUI animations. Omitted IDs use the current node, typed item key, data context, caller location and occurrence within that call site. Call these helpers from the same locations in both passes; use `ItemKey` for reorderable collections.

```csharp
foreach (var item in items)
{
    using var identity = gui.ItemKey(item.Id);
    using var data = gui.EnterDataScope();
    ref var expanded = ref gui.GetData(false);
    gui.Checkbox(ref expanded, item.Name);
    var visibility = gui.AnimateBool01(expanded, 0.2f, Easing.Linear);
}
```

## Nested contexts

`using var data = gui.EnterDataScope("tooltip")` enters a child of the active data context and restores that context on disposal. Any non-null typed key can be used; keys compare by type and value, and hash collisions do not alias contexts. Entering the same key again in the same outer data and item context intentionally returns to the same stored data. Repeated independent instances should use automatic scopes or separate item keys. Dispose scopes in reverse order, inside the node where they were entered, before changing passes or starting another frame.

`gui.SetDataScope("popup")` selects a context relative to the context inherited when the current node was submitted. Descendant nodes inherit it; leaving the node restores the surrounding context. Repeating the same selection does not create additional nesting. `SetDataScope()` selects an automatic context. A node remembers its selected context when re-entered, allowing returned popup content containers to be filled later. Submitting that node again resets its inherited data context for the new pass.

Scoped node IDs, `AutomaticId`, `NodeId`, form and control identities all include the data context. Explicit node IDs remain stylesheet selector IDs, while exported identity strings become opaque within a data scope. Internal control stores also include the context, so explicitly named popup and tooltip state cannot cross scope boundaries. Query or clear control state using the documented control APIs; queries for a scoped control must run in its owning context.

## Named sharing and global compatibility

`gui.GetData(0, "counter")` and `gui.SetData(5, "counter")` intentionally share a value of the same type across call sites and layout nodes in the current data and item context. Different types have separate stores. Omitting an ID creates an automatic call identity, so unrelated `GetData` and `SetData` calls do not automatically refer to the same value; retain the returned reference or supply a named key when linking calls.

Existing `GetValue`, `SetValue`, `GetFloat` and `SetFloat` remain GUI-wide and retain their caller-expression string keys, even inside a data scope. Existing callers need no migration for these APIs. Scoped data is an independent store: replace paired global calls with `GetData`/`SetData` and an explicit shared key to opt into isolation. The stable-reference guarantee applies to `GetData`; legacy references into dictionary entries must not be held across dictionary growth.

## Frame updates

`gui.UpdateData(0, count => count + 1, "counter")` runs the callback on the first access in each frame and returns that update's snapshot for all subsequent accesses, including the render pass. The first call's default and callback are authoritative. Writes through `GetData` references or `SetData` are immediate; callers control their mutation timing. Use `UpdateData` for work that must advance exactly once per frame. For mutable reference types its returned snapshot is the same object, so mutations to that object remain visible.

Within a frame, `gui.AnimateBool01(target, duration, easing)` uses automatic scoped identity and samples only once per frame. `gui.AnimateBool01("visibility", target, duration, easing)` intentionally shares an animation within a data and item context. The first access determines that frame's target, duration and easing; a different target requested in the render pass takes effect when requested in a later frame. Standalone `AnimationFloat` and `AnimationManager` retain their existing contracts, as do calls to the original unnamed GUI animation helper outside a frame.

**Animation behavior migration:** calls at the same source line during a frame now create separate occurrence identities and include their parent, item and data context. Use a named animation when sharing across call sites is intentional. Build the same calls in both passes, and forward caller metadata through custom wrappers. This behavior change does not alter the original method signature.

## Lifetime

Hidden, removed and reappearing nodes retain their data and control state for the lifetime of the `Gui`; absence does not evict state. Reappearing keyed items recover their previous values. Hidden animations retain their targets and use elapsed GUI time when sampled again, so completed transitions resume at the target. No background updates are performed for absent data callbacks.

Render properties such as font size, color and z-index continue to inherit through `LayoutNodeScope` within the current frame. They are restored when a node exits and rebuilt with the layout tree each frame; persisted data values do not inherit through that style store or prolong its lifetime.

`ClearData()` forgets all scoped data. `ClearAnimations()` clears managed GUI animations, and `ClearControlStates()` clears control state; these stores are independent. Clearing does not dispose caller-owned values. `GetData` returns a reference backed by a stable cell, so inserting other keys cannot invalidate it. After `ClearData`, existing references remain safe to access but point to detached cells; reacquire them to access newly initialized data. Scope disposal only restores context and never deletes values. Identity descriptors and typed keys remain retained until the GUI is collected; automatic eviction and retained layout nodes are separate concerns.

The nested-context model is inspired by [PanGui's data scopes](https://www.pangui.io/); these APIs describe Guinevere's implementation and lifetime rules.
