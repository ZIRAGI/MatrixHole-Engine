using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace MatrixHole.Core
{
    /// <summary>
    /// FacilityMap — интерактивная карта комплекса (п.51 OptimizatorPlan).
    /// Heavy/Light/Entrance/Surface с обозначением спавнов, оружейных, генераторов.
    /// </summary>
    public static class FacilityMap
    {
        public enum Zone { Surface, Entrance, LightContainment, HeavyContainment }

        public class MapPoint
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public PointType Type { get; set; }
            public Zone Zone { get; set; }
            public double X { get; set; }
            public double Y { get; set; }
            public string? Description { get; set; }
        }

        public enum PointType
        {
            SpawnClassD, SpawnScientist, SpawnGuard, SpawnMTF, SpawnChaos, SpawnSCP,
            WeaponLocker, MedicalLocker, Generator, Elevator, Intercom, Warhead, Checkpoint, Other
        }

        public static List<MapPoint> GetDefaultPoints()
        {
            return new List<MapPoint>
            {
                // Surface
                new() { Id = "s_gate_a", Name = "Gate A", Type = PointType.Other, Zone = Zone.Surface, X = 30, Y = 20 },
                new() { Id = "s_gate_b", Name = "Gate B", Type = PointType.Other, Zone = Zone.Surface, X = 70, Y = 20 },
                new() { Id = "s_escape", Name = "Escape Zone", Type = PointType.Other, Zone = Zone.Surface, X = 50, Y = 10 },
                new() { Id = "s_mtf_spawn", Name = "MTF Spawn", Type = PointType.SpawnMTF, Zone = Zone.Surface, X = 25, Y = 15 },
                new() { Id = "s_chaos_spawn", Name = "Chaos Spawn", Type = PointType.SpawnChaos, Zone = Zone.Surface, X = 75, Y = 15 },

                // Entrance
                new() { Id = "e_check_a", Name = "Checkpoint A", Type = PointType.Checkpoint, Zone = Zone.Entrance, X = 40, Y = 40 },
                new() { Id = "e_check_b", Name = "Checkpoint B", Type = PointType.Checkpoint, Zone = Zone.Entrance, X = 60, Y = 40 },
                new() { Id = "e_intercom", Name = "Intercom", Type = PointType.Intercom, Zone = Zone.Entrance, X = 50, Y = 35 },
                new() { Id = "e_guard_spawn", Name = "Guard Spawn", Type = PointType.SpawnGuard, Zone = Zone.Entrance, X = 45, Y = 45 },
                new() { Id = "e_elev_a", Name = "Elevator A", Type = PointType.Elevator, Zone = Zone.Entrance, X = 35, Y = 38 },
                new() { Id = "e_elev_b", Name = "Elevator B", Type = PointType.Elevator, Zone = Zone.Entrance, X = 65, Y = 38 },

                // Light
                new() { Id = "l_classd_spawn", Name = "Class-D Cells", Type = PointType.SpawnClassD, Zone = Zone.LightContainment, X = 30, Y = 60 },
                new() { Id = "l_scientist_spawn", Name = "Scientist Spawn", Type = PointType.SpawnScientist, Zone = Zone.LightContainment, X = 70, Y = 60 },
                new() { Id = "l_medical", Name = "Medical Room", Type = PointType.MedicalLocker, Zone = Zone.LightContainment, X = 50, Y = 55 },
                new() { Id = "l_weapon", Name = "Armory (Light)", Type = PointType.WeaponLocker, Zone = Zone.LightContainment, X = 40, Y = 65 },
                new() { Id = "l_914", Name = "SCP-914", Type = PointType.Other, Zone = Zone.LightContainment, X = 60, Y = 65 },

                // Heavy
                new() { Id = "h_nuke", Name = "Warhead Silo", Type = PointType.Warhead, Zone = Zone.HeavyContainment, X = 50, Y = 85 },
                new() { Id = "h_gen_1", Name = "Generator 1", Type = PointType.Generator, Zone = Zone.HeavyContainment, X = 30, Y = 80 },
                new() { Id = "h_gen_2", Name = "Generator 2", Type = PointType.Generator, Zone = Zone.HeavyContainment, X = 40, Y = 75 },
                new() { Id = "h_gen_3", Name = "Generator 3", Type = PointType.Generator, Zone = Zone.HeavyContainment, X = 60, Y = 75 },
                new() { Id = "h_gen_4", Name = "Generator 4", Type = PointType.Generator, Zone = Zone.HeavyContainment, X = 70, Y = 80 },
                new() { Id = "h_scp_106", Name = "SCP-106", Type = PointType.SpawnSCP, Zone = Zone.HeavyContainment, X = 25, Y = 85 },
                new() { Id = "h_scp_049", Name = "SCP-049", Type = PointType.SpawnSCP, Zone = Zone.HeavyContainment, X = 75, Y = 85 },
                new() { Id = "h_micro", Name = "MicroHID", Type = PointType.WeaponLocker, Zone = Zone.HeavyContainment, X = 50, Y = 90 },
            };
        }

        public static string GetMapDataJson()
        {
            return JsonConvert.SerializeObject(new
            {
                zones = Enum.GetNames(typeof(Zone)),
                pointTypes = Enum.GetNames(typeof(PointType)),
                points = GetDefaultPoints()
            });
        }
    }
}
