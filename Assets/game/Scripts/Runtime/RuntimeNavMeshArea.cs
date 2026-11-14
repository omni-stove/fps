using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Runtime
{
    // 床を GameBootstrap が実行時に作るシーン (TPS の射撃場など) では、シーンを作るときに NavMesh を焼けない。
    // そこで床ができた後 (Start) にこの範囲だけ焼き、NavMesh が無いまま動き出さないよう、焼いてから敵などを有効にする。
    [DisallowMultipleComponent]
    public sealed class RuntimeNavMeshArea : MonoBehaviour
    {
        public const string LogTag = "[NavMeshArea]";

        [SerializeField, Tooltip("焼く範囲の大きさ (m)。中心はこの GameObject の位置")]
        private Vector3 _size = new Vector3(10f, 4f, 10f);

        [SerializeField, Tooltip("焼く NavMesh の Agent Type")]
        private int _agentTypeId;

        [SerializeField, Tooltip("焼いた後に有効にするもの。シーンでは無効にしておく")]
        private GameObject[] _activateAfterBuild = Array.Empty<GameObject>();

        private NavMeshData _data;
        private NavMeshDataInstance _instance;

        public Vector3 Size
        {
            get => _size;
            set => _size = value;
        }

        public int AgentTypeId
        {
            get => _agentTypeId;
            set => _agentTypeId = value;
        }

        public IReadOnlyList<GameObject> ActivateAfterBuild
        {
            get => _activateAfterBuild ??= Array.Empty<GameObject>();
            set
            {
                if (value == null)
                {
                    _activateAfterBuild = Array.Empty<GameObject>();
                    return;
                }

                _activateAfterBuild = new GameObject[value.Count];
                for (var i = 0; i < value.Count; i++)
                {
                    _activateAfterBuild[i] = value[i];
                }
            }
        }

        public bool IsBuilt => _instance.valid;

        public Bounds WorldBounds => new Bounds(transform.position, _size);

        public bool Build()
        {
            if (_instance.valid)
            {
                return true;
            }

            if (!(_size.x > 0f && _size.y > 0f && _size.z > 0f))
            {
                Debug.LogError($"{LogTag} '{name}' の大きさ {_size} は正でなければなりません。", this);
                return false;
            }

            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(
                WorldBounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);

            var settings = NavMesh.GetSettingsByID(_agentTypeId);
            _data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.zero, _size), transform.position, Quaternion.identity);
            if (_data == null)
            {
                Debug.LogError($"{LogTag} '{name}' の NavMesh を焼けませんでした (sources={sources.Count})。", this);
                return false;
            }

            _instance = NavMesh.AddNavMeshData(_data);
            if (!_instance.valid)
            {
                Debug.LogError($"{LogTag} '{name}' の NavMesh を加えられませんでした。", this);
                Destroy(_data);
                _data = null;
                return false;
            }

            Debug.Log($"{LogTag} '{name}' built from {sources.Count} sources", this);
            return true;
        }

        private void Start()
        {
            if (!Build())
            {
                return;
            }

            foreach (var target in ActivateAfterBuild)
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

            if (_data != null)
            {
                Destroy(_data);
            }
        }
    }
}
