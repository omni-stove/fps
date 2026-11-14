using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    // 床を実行時に作るシーン (GameBootstrap の射撃場) には焼いた NavMesh を置けないので、自分の子のコライダーだけから Play の頭に焼く。
    // 焼いたアセットを持たないので、シーンを作り直すたびに NavMesh のアセットを作り直さずに済む。
    // NavMeshAgent は有効になった瞬間に NavMesh を探すので、敵は無効にして置き、焼いた後に有効にする。
    [DisallowMultipleComponent]
    public sealed class RuntimeNavMesh : MonoBehaviour
    {
        public const string LogTag = "[RuntimeNavMesh]";

        // 段差や体の高さぶん、コライダーの範囲より上下に広く焼く。
        public const float BoundsMargin = 2f;

        [SerializeField, Tooltip("焼くエージェントの種類 (0 は Humanoid)")]
        private int _agentTypeId;

        [SerializeField, Tooltip("焼いた後に有効にするもの (NavMeshAgent を持つ敵など)")]
        private GameObject[] _activateAfterBake = Array.Empty<GameObject>();

        private NavMeshDataInstance _instance;

        public int AgentTypeId
        {
            get => _agentTypeId;
            set => _agentTypeId = value;
        }

        public IReadOnlyList<GameObject> ActivateAfterBake
        {
            get => _activateAfterBake;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                var copy = new GameObject[value.Count];
                for (var i = 0; i < copy.Length; i++)
                {
                    copy[i] = value[i] != null ? value[i] : throw new ArgumentException($"entry {i} is null", nameof(value));
                }

                _activateAfterBake = copy;
            }
        }

        public bool IsBaked => _instance.valid;

        private void Awake()
        {
            if (!Bake())
            {
                return;
            }

            foreach (var target in _activateAfterBake)
            {
                if (target != null)
                {
                    target.SetActive(true);
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance.valid)
            {
                _instance.Remove();
            }
        }

        public bool Bake()
        {
            if (_instance.valid)
            {
                return true;
            }

            if (!TryGetColliderBounds(out var bounds))
            {
                Debug.LogError($"{LogTag} '{name}' の子に有効なコライダーが無いので NavMesh を焼けません。", this);
                return false;
            }

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(
                transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            bounds.Expand(new Vector3(BoundsMargin, BoundsMargin, BoundsMargin) * 2f);

            var data = NavMeshBuilder.BuildNavMeshData(
                NavMesh.GetSettingsByID(_agentTypeId), sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null)
            {
                Debug.LogError($"{LogTag} '{name}' の NavMesh を焼けませんでした (sources={sources.Count}).", this);
                return false;
            }

            _instance = NavMesh.AddNavMeshData(data);
            if (!_instance.valid)
            {
                Debug.LogError($"{LogTag} '{name}' の焼いた NavMesh を加えられませんでした。", this);
                return false;
            }

            Debug.Log($"{LogTag} baked '{name}' sources={sources.Count} bounds={bounds.center.ToString("F1")}/{bounds.size.ToString("F1")}", this);
            return true;
        }

        private bool TryGetColliderBounds(out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var collider in GetComponentsInChildren<Collider>())
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
