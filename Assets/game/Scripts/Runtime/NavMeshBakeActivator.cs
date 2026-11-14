using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    // 床を実行時に作るシーン (GameBootstrap) では焼いた NavMesh を保存できないので、置いた足場から Play 中に焼き、
    // 焼き終えてから敵を有効にする。先に有効にすると NavMeshAgent が NavMesh に乗れずエラーになる。
    // Fps.Runtime は AI Navigation パッケージを参照しないので、NavMeshSurface ではなくエンジンの NavMeshBuilder で焼く。
    [DisallowMultipleComponent]
    public sealed class NavMeshBakeActivator : MonoBehaviour
    {
        public const string LogTag = "[NavMeshBake]";

        [SerializeField, Tooltip("この下の有効なコライダーから NavMesh を焼く")]
        private Transform _source;

        [SerializeField, Tooltip("焼いた後に有効にするもの (敵など)")]
        private GameObject[] _activateAfterBake = Array.Empty<GameObject>();

        [SerializeField, Tooltip("焼く NavMesh のエージェントの種類")]
        private int _agentTypeId;

        private NavMeshData _data;
        private NavMeshDataInstance _instance;

        public Transform Source => _source;

        public IReadOnlyList<GameObject> ActivateAfterBake => _activateAfterBake;

        public bool IsBaked => _instance.valid;

        private void Awake()
        {
            Bake();
        }

        private void OnDestroy()
        {
            if (_instance.valid)
            {
                _instance.Remove();
            }

            if (_data != null)
            {
                Destroy(_data);
            }
        }

        public void Configure(Transform source, IReadOnlyList<GameObject> activateAfterBake, int agentTypeId)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (activateAfterBake == null)
            {
                throw new ArgumentNullException(nameof(activateAfterBake));
            }

            _source = source;
            _activateAfterBake = new GameObject[activateAfterBake.Count];
            for (var i = 0; i < activateAfterBake.Count; i++)
            {
                _activateAfterBake[i] = activateAfterBake[i];
            }

            _agentTypeId = agentTypeId;
        }

        public bool Bake()
        {
            if (_instance.valid)
            {
                return true;
            }

            if (_source == null)
            {
                Debug.LogError($"{LogTag} '{name}' has no source to bake from", this);
                return false;
            }

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(
                _source, Physics.DefaultRaycastLayers, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            if (sources.Count == 0 || !TryGetBounds(out var bounds))
            {
                Debug.LogError($"{LogTag} '{name}' found no colliders under '{_source.name}' to bake", this);
                return false;
            }

            // 足場の上に立つ高さの分だけ上に広げる。広げないと上面の NavMesh が範囲の端で切れる。
            bounds.Expand(new Vector3(1f, 4f, 1f));
            _data = NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByID(_agentTypeId), sources, bounds, Vector3.zero, Quaternion.identity);
            _instance = NavMesh.AddNavMeshData(_data);
            if (!_instance.valid)
            {
                Debug.LogError($"{LogTag} '{name}' could not add the baked NavMesh", this);
                return false;
            }

            foreach (var target in _activateAfterBake)
            {
                if (target != null)
                {
                    target.SetActive(true);
                }
            }

            Debug.Log($"{LogTag} baked id={name} sources={sources.Count} activated={_activateAfterBake.Length}", this);
            return true;
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var collider in _source.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(collider.bounds);
                }
                else
                {
                    bounds = collider.bounds;
                    found = true;
                }
            }

            return found;
        }
    }
}
