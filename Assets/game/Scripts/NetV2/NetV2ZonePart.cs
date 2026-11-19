using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.Rendering;

namespace Shooter.NetV2
{
    // オフラインの ShrinkingZone と同じ筒で、今の円と次の円をスナップショットの値で出す。縮めるのもダメージもサーバーがするので、ここは見せるだけ。
    // 見た目 (プレハブ・材質・色・筒の高さ・地面の高さ) はシーンの ShrinkingZone から取り、無ければ ShrinkingZone の既定で作る。
    // 次の円は ShrinkingZone のギズモと同じく白で描き、閉じた後 (Closed) と今の円に重なったときは隠す
    public sealed class NetV2ZonePart : INetV2ClientPart
    {
        public const string RootName = "NetV2Zone";
        public const string NextBoundaryName = "NextZoneBoundary";

        public static readonly Color NextBoundaryColor = Color.white;

        private readonly ClientZone _zone = new ClientZone();
        private readonly Func<ShrinkingZone> _findLook;
        private GameObject _root;
        private ShrinkingZone _look;
        private Ring _current;
        private Ring _next;
        private bool _disposed;

        public NetV2ZonePart(Func<ShrinkingZone> findLook = null)
        {
            _findLook = findLook ?? (() => UnityEngine.Object.FindAnyObjectByType<ShrinkingZone>());
        }

        public ZoneState? State => _zone.State;

        public GameObject Root => _root;

        public GameObject CurrentBoundary => _current?.Visual;

        public GameObject NextBoundary => _next?.Visual;

        public bool Handle(IMessage message)
        {
            return false;
        }

        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            _zone.Apply(snapshot.Match);
            var state = _zone.State;
            if (!state.HasValue)
            {
                Release();
                return;
            }

            // マップが替わるとシーンごと筒も消えるので、Unity の破棄済みの参照なら作り直す
            if (_root == null)
            {
                Build();
            }

            var zone = state.Value;
            _current.Place(zone.Center, zone.Radius);
            var showNext = zone.Stage != ZoneStage.Closed && (zone.TargetCenter != zone.Center || zone.TargetRadius != zone.Radius);
            _next.Visual.SetActive(showNext);
            if (showNext)
            {
                _next.Place(zone.TargetCenter, zone.TargetRadius);
            }

            _current.Paint(CurrentColor);
            _next.Paint(NextColor);
        }

        public void Tick(float deltaSeconds)
        {
        }

        public void Dispose()
        {
            _disposed = true;
            Release();
        }

        private Color CurrentColor => _look != null
            ? _look.BoundaryColorFor(TeamColors.Current)
            : DangerColor(ShrinkingZone.DefaultBoundaryColor.a);

        private Color NextColor => new Color(NextBoundaryColor.r, NextBoundaryColor.g, NextBoundaryColor.b, CurrentColor.a);

        private static Color DangerColor(float alpha)
        {
            var danger = TeamColors.ToColor(TeamColors.Current.Danger);
            return new Color(danger.r, danger.g, danger.b, alpha);
        }

        private void Build()
        {
            Release();
            _look = _findLook();
            _root = new GameObject(RootName);
            var height = _look != null ? _look.BoundaryHeight : ShrinkingZone.DefaultBoundaryHeight;
            var groundY = _look != null ? _look.transform.position.y : 0f;
            var prefab = _look != null ? _look.BoundaryPrefab : null;
            var material = _look != null ? _look.BoundaryMaterial : null;
            _current = new Ring(_root.transform, ShrinkingZone.BoundaryName, prefab, material, height, groundY);
            _next = new Ring(_root.transform, NextBoundaryName, prefab, material, height, groundY);
        }

        private void Release()
        {
            _current?.Release();
            _next?.Release();
            _current = null;
            _next = null;
            if (_root != null)
            {
                DestroySafely(_root);
            }

            _root = null;
            _look = null;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        // ShrinkingZone.CreateBoundary と同じ作り方。差し替えた材質とプレハブはゲームが決めた見た目なので塗らない
        private sealed class Ring
        {
            private readonly float _height;
            private readonly float _groundY;
            private readonly Mesh _mesh;
            private readonly Material _material;

            public Ring(Transform parent, string name, GameObject prefab, Material material, float height, float groundY)
            {
                _height = height;
                _groundY = groundY;
                if (prefab != null)
                {
                    Visual = VisualPrefab.Instantiate(prefab, parent, name, stripColliders: true);
                    foreach (var prefabRenderer in Visual.GetComponentsInChildren<Renderer>(true))
                    {
                        prefabRenderer.shadowCastingMode = ShadowCastingMode.Off;
                        prefabRenderer.receiveShadows = false;
                    }

                    return;
                }

                Visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Visual.name = name;

                // Destroy はフレーム末まで残り、その間に拡大した Collider が移動や射撃の判定に当たるため即時に消す
                UnityEngine.Object.DestroyImmediate(Visual.GetComponent<Collider>());
                Visual.transform.SetParent(parent, false);
                _mesh = ShrinkingZone.BuildTubeMesh(ShrinkingZone.BoundarySegments);
                Visual.GetComponent<MeshFilter>().sharedMesh = _mesh;
                var renderer = Visual.GetComponent<Renderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (material != null)
                {
                    renderer.sharedMaterial = material;
                    return;
                }

                _material = ShrinkingZone.CreateBoundaryMaterial(renderer.sharedMaterial);
                renderer.sharedMaterial = _material;
            }

            public GameObject Visual { get; }

            // 筒のメッシュは半径 1・高さ 1 なので、世界で半径 r・高さ _height になるよう拡大する
            public void Place(System.Numerics.Vector2 center, float radius)
            {
                Visual.transform.SetPositionAndRotation(new Vector3(center.X, _groundY, center.Y), Quaternion.identity);
                Visual.transform.localScale = new Vector3(radius, _height, radius);
            }

            public void Paint(Color color)
            {
                if (_material != null && _material.color != color)
                {
                    _material.color = color;
                }
            }

            public void Release()
            {
                DestroySafely(_material);
                DestroySafely(_mesh);
            }
        }
    }
}
