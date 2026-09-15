using System.Collections.Generic;
using Unity.Mathematics;

namespace Project.World.Editing
{
    /// <summary>
    /// Six-face flood fill from the voxels exposed by a dig. A component that neither reaches
    /// bedrock nor exceeds the collapse threshold is considered unsupported and falls.
    /// </summary>
    public sealed class ConnectivitySystem
    {
        private readonly VoxelEditService _edit;
        private readonly int _threshold;

        private readonly Queue<int3> _frontier = new Queue<int3>();
        private readonly HashSet<int3> _visited = new HashSet<int3>();
        private readonly HashSet<int3> _settled = new HashSet<int3>();
        private readonly List<int3> _component = new List<int3>();
        private readonly List<int3> _seeds = new List<int3>();

        public ConnectivitySystem(VoxelEditService edit, int threshold)
        {
            _edit = edit;
            _threshold = math.max(1, threshold);
        }

        /// <summary>
        /// Appends every unsupported component touching <paramref name="removed"/> to
        /// <paramref name="islands"/>. Each island is a fresh list owned by the caller.
        /// </summary>
        public void FindUnsupported(List<int3> removed, List<List<int3>> islands)
        {
            _settled.Clear();
            CollectSeeds(removed);

            for (int i = 0; i < _seeds.Count; i++)
            {
                int3 seed = _seeds[i];
                if (_settled.Contains(seed))
                {
                    continue;
                }

                if (Flood(seed, out bool anchored) && !anchored)
                {
                    islands.Add(new List<int3>(_component));
                }
            }
        }

        private void CollectSeeds(List<int3> removed)
        {
            _seeds.Clear();

            for (int i = 0; i < removed.Count; i++)
            {
                int3 origin = removed[i];

                for (int face = 0; face < 6; face++)
                {
                    int3 neighbour = origin + Face(face);
                    if (_edit.IsSupporting(neighbour))
                    {
                        _seeds.Add(neighbour);
                    }
                }
            }
        }

        private bool Flood(int3 seed, out bool anchored)
        {
            _visited.Clear();
            _component.Clear();
            _frontier.Clear();

            _visited.Add(seed);
            _frontier.Enqueue(seed);
            anchored = false;

            while (_frontier.Count > 0)
            {
                int3 current = _frontier.Dequeue();

                // Bedrock row anchors everything above it.
                if (current.y <= 1)
                {
                    anchored = true;
                    break;
                }

                _component.Add(current);
                if (_component.Count > _threshold)
                {
                    anchored = true;
                    break;
                }

                for (int face = 0; face < 6; face++)
                {
                    int3 neighbour = current + Face(face);
                    if (_visited.Contains(neighbour))
                    {
                        continue;
                    }

                    // Never collapse across the streaming border: unloaded ground counts as support.
                    if (!_edit.IsLoaded(neighbour))
                    {
                        anchored = true;
                        _frontier.Clear();
                        break;
                    }

                    if (!_edit.IsSupporting(neighbour))
                    {
                        continue;
                    }

                    _visited.Add(neighbour);
                    _frontier.Enqueue(neighbour);
                }
            }

            foreach (int3 visited in _visited)
            {
                _settled.Add(visited);
            }

            return _component.Count > 0;
        }

        private static int3 Face(int index)
        {
            switch (index)
            {
                case 0: return new int3(1, 0, 0);
                case 1: return new int3(-1, 0, 0);
                case 2: return new int3(0, 1, 0);
                case 3: return new int3(0, -1, 0);
                case 4: return new int3(0, 0, 1);
                default: return new int3(0, 0, -1);
            }
        }
    }
}
