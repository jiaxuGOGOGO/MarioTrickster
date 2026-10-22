using System.Collections.Generic;
using System.Linq;

// OverworldTown supplies this extension in Unity-dependent gameplay code. The map subsystem only
// needs deterministic door ordering here, so the harness supplies the same pure behavior locally.
public static class WorldParkHarnessDoorOrder
{
    public static IEnumerable<OverworldMap.Door> OrderBy2(this IEnumerable<OverworldMap.Door> doors)
    {
        return doors.OrderBy(d => d.minute).ThenBy(d => d.n);
    }
}
