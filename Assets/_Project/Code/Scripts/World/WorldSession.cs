using System;
using UnityEngine;

namespace Project.World
{
    public sealed class WorldSession
    {
        public WorldSession(WorldGenSettings settings, string worldId = null)
        {
            Settings = settings ? settings : throw new ArgumentNullException(nameof(settings));
            Seed = settings.Seed;
            WorldId = string.IsNullOrWhiteSpace(worldId) ? $"seed-{Seed}" : worldId;
        }

        public WorldGenSettings Settings { get; }
        public int Seed { get; }
        public string WorldId { get; }
    }
}
