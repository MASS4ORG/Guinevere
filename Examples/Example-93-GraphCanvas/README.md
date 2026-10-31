# Graph canvas scale benchmark

Purpose: benchmark canvas scaling and virtualization with 10,000 view tiles, independently of the showcase.

Run `dotnet run --project Examples/Example-93-GraphCanvas` from the repository root. The sample builds 10,000 view tiles using Guinevere core and the Silk.NET host; it does not reference Excalibur or create a parallel graph model. Zoom out to see the overview, press Home to frame all tiles, or use arrows to inspect and reveal tiles with the keyboard. Middle-drag pans, the wheel zooms around the cursor, and the minimap recenters the camera.
