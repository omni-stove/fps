using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.NetV2
{
    // 見た目は置く物の定義 (DeployAbilityDefinition) の Prefab を差し替えの参照として使い、空なら定義の大きさの箱を作る。
    // 定義の無い種類も、種類 (Kind) ごとの既定の大きさの箱で出す。サーバーにある物を見えなくしないため。
    public sealed class NetV2DeployablesPart : INetV2ClientPart, IClientDeployableViews
    {
        private readonly ClientDeployables _deployables;
        private readonly Func<int> _localPlayerId;
        private readonly Action<string> _log;
        private readonly Dictionary<string, DeployAbilityDefinition> _definitions =
            new Dictionary<string, DeployAbilityDefinition>(StringComparer.Ordinal);
        private bool _disposed;

        public NetV2DeployablesPart(
            Func<IMessage, bool> send, Func<int> localPlayerId, IEnumerable<DeployAbilityDefinition> definitions, Action<string> log = null)
        {
            _localPlayerId = localPlayerId ?? throw new ArgumentNullException(nameof(localPlayerId));
            _log = log ?? (line => Debug.Log(line));
            _deployables = new ClientDeployables(send ?? throw new ArgumentNullException(nameof(send)), _log);
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    if (definition != null && !string.IsNullOrEmpty(definition.Id) && !_definitions.ContainsKey(definition.Id))
                    {
                        _definitions.Add(definition.Id, definition);
                    }
                }
            }

            _deployables.Attach(this);
        }

        public ClientDeployables Deployables => _deployables;

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (!_disposed)
            {
                _deployables.Apply(snapshot, _localPlayerId());
            }
        }

        public void Tick(float deltaSeconds)
        {
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _deployables.Detach();
        }

        // 置けるかはオフラインと同じ調べ方 (定義の Survey) で手元で見て、置ける所だけを送る。届く距離と足場はサーバーが確かめ直す
        public bool Deploy(string typeId, Transform owner, Vector3 aimOrigin, Vector3 aimDirection)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (_disposed)
            {
                return false;
            }

            if (string.IsNullOrEmpty(typeId) || !_definitions.TryGetValue(typeId, out var definition))
            {
                _log(ClientDeployLog.UnknownType(typeId));
                return false;
            }

            var verdict = definition.Survey(owner, aimOrigin, aimDirection, definition.ToDeploySpec(), out var pose, out _);
            if (verdict != DeployVerdict.Ok)
            {
                _log(ClientDeployLog.NotSent(typeId, verdict));
                return false;
            }

            return _deployables.Request(typeId, NetV2Math.ToNumerics(pose.position), pose.rotation.eulerAngles.y);
        }

        public IClientDeployableView Show(DeployableKind kind, string typeId, Team team, bool isOwn, System.Numerics.Vector3 position, float yaw)
        {
            _definitions.TryGetValue(typeId ?? "", out var definition);
            var turret = definition as TurretDeployDefinition;
            var visual = definition != null && definition.Prefab != null
                ? UnityEngine.Object.Instantiate(definition.Prefab)
                : turret != null ? turret.CreateLook() : CreatePlaceholder(kind, definition);
            visual.name = "Net" + (string.IsNullOrEmpty(typeId) ? kind.ToString() : typeId);
            Quiet(visual);
            // タレットのスナップショットの向きは頭の向きなので、台は回さず頭だけに当てる
            var head = turret != null ? FindHead(visual.transform) : null;
            var view = new View(visual, head);
            view.Move(position, yaw);
            _log(ClientDeployLog.Look(typeId, kind.ToString(), definition != null && definition.Prefab != null));
            return view;
        }

        // 置いた物の差し替えの Prefab には、オフラインで自分で動く部品 (Turret・Decoy・Deployable) と当たりが付いている。
        // 動くかと当たりはサーバーが決めるので、手元では止める。当たりを残すと、それを知らないサーバーの移動と手元の予測がずれる
        private static void Quiet(GameObject visual)
        {
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is Turret || behaviour is Decoy || behaviour is Deployable)
                {
                    behaviour.enabled = false;
                }
            }

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var obstacle in visual.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                obstacle.enabled = false;
            }
        }

        private static Transform FindHead(Transform root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == Turret.HeadName)
                {
                    return child;
                }
            }

            return null;
        }

        private static GameObject CreatePlaceholder(DeployableKind kind, DeployAbilityDefinition definition)
        {
            var size = definition != null ? definition.Size : DefaultSize(kind);
            var root = new GameObject();
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Box";
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
            box.transform.localScale = size;
            return root;
        }

        private static Vector3 DefaultSize(DeployableKind kind)
        {
            var spec = DeployType.DefaultDeploy(RoleOf(kind));
            return new Vector3(spec.Width, spec.Height, spec.Depth);
        }

        private static DeployRole RoleOf(DeployableKind kind)
        {
            switch (kind)
            {
                case DeployableKind.Turret:
                    return DeployRole.Turret;
                case DeployableKind.Decoy:
                    return DeployRole.Decoy;
                default:
                    return DeployRole.Shield;
            }
        }

        public sealed class View : IClientDeployableView
        {
            private readonly GameObject _visual;

            private readonly Transform _head;

            internal View(GameObject visual, Transform head = null)
            {
                _visual = visual;
                _head = head;
            }

            public GameObject Visual => _visual;

            public int Health { get; private set; }

            public void Move(System.Numerics.Vector3 position, float yaw)
            {
                if (_visual != null)
                {
                    var rotation = Quaternion.Euler(0f, yaw, 0f);
                    if (_head != null)
                    {
                        _visual.transform.SetPositionAndRotation(NetV2Math.ToUnity(position), Quaternion.identity);
                        _head.rotation = rotation;
                        return;
                    }

                    _visual.transform.SetPositionAndRotation(NetV2Math.ToUnity(position), rotation);
                }
            }

            public void SetHealth(int health)
            {
                Health = health;
            }

            // Edit Mode (テスト) では Destroy を呼べないので、すぐに消す
            public void Remove()
            {
                if (_visual == null)
                {
                    return;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_visual);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_visual);
                }
            }
        }
    }
}
