# Graph canvas stress sample

Purpose: prove canvas scaling and virtualization with 10,000 view tiles.

Run `dotnet run --project Examples/Example-05-GraphCanvas` from the repository root. The sample builds 10,000 view tiles using Guinevere core and the Silk.NET host; it does not reference Excalibur or create a parallel graph model. Zoom out to see the overview, press Home to frame all tiles, or use arrows to inspect and reveal tiles with the keyboard. Middle-drag pans, the wheel zooms around the cursor, and the minimap recenters the camera.

The rectangles exercise view construction, culling, stable identity, and navigation. Document-backed nodes, ports, links, registry search, reversible edits, and the FSM sample wait for the headless Graph track.
