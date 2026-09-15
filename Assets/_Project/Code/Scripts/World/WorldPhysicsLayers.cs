using UnityEngine;

namespace Project.World
{
    public static class WorldPhysicsLayers
    {
        public const string TerrainName = "Terrain";
        public const string FallingTerrainName = "FallingTerrain";
        public const string PlayerName = "Player";

        public static int Terrain { get; private set; }
        public static int FallingTerrain { get; private set; }
        public static int Player { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Cache()
        {
            Terrain = LayerMask.NameToLayer(TerrainName);
            FallingTerrain = LayerMask.NameToLayer(FallingTerrainName);
            Player = LayerMask.NameToLayer(PlayerName);
        }
    }
}
