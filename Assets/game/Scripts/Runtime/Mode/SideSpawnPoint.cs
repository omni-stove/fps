using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SideSpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0), Tooltip("この点から出る陣営の ID")]
        private int _sideId;

        public Side Side
        {
            get => new Side(Mathf.Max(0, _sideId));
            set => _sideId = value.Id;
        }

        public Vector3 Position => transform.position;

        public Quaternion Rotation => transform.rotation;

        public static SideSpawnPoint[] FindAll()
        {
            return FindObjectsByType<SideSpawnPoint>(FindObjectsInactive.Exclude);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.HSVToRGB(Mathf.Repeat(_sideId * 0.37f, 1f), 0.8f, 1f);
            Gizmos.DrawWireSphere(transform.position + (Vector3.up * 0.1f), 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward);
        }
    }
}
