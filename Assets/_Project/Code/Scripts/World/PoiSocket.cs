using UnityEngine;

namespace Project.World
{
    /// <summary>
    /// Reserved spot for a point of interest. Deliberately empty: the slice only proves that the
    /// generator can place and find one, not what goes in it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PoiSocket : MonoBehaviour
    {
        [SerializeField] private string _socketId = "poi.default";

        public string SocketId => _socketId;
    }
}
