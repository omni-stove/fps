using System;
using UnityEngine;

namespace Shooter.Runtime
{
    // 立っているかは ExtractionRunner が毎回位置で確かめる。トリガーの出入りで数えると、倒れて消えた人の出口が届かず残ることがある。
    [DisallowMultipleComponent]
    public sealed class ExtractionZone : MonoBehaviour
    {
        public const float DefaultRadius = 3f;
        public const float MinRadius = 0.1f;
        public const string VisualName = "Visual";

        // 床に描く印なので、足が埋まらない薄さにする。
        public const float PlaceholderThickness = 0.02f;

        public static readonly Color DefaultClosedColor = new Color(0.4f, 0.4f, 0.4f);
        public static readonly Color DefaultOpenColor = new Color(0.2f, 0.75f, 0.3f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("脱出地点の Id。シーンの中で重ならないこと。開いた地点の抽選とログはこれで引く")]
        private string _pointId = string.Empty;

        [SerializeField, Min(MinRadius), Tooltip("この水平の距離 (m) 以内に立てば地点にいるとみなす")]
        private float _radius = DefaultRadius;

        [SerializeField, Tooltip("閉じている間の見た目。原点が地面の中心。空なら床に灰色の円を描く")]
        private GameObject _closedVisualPrefab;

        [SerializeField, Tooltip("開いている間の見た目。空なら床に緑の円を描く")]
        private GameObject _openVisualPrefab;

        public string PointId
        {
            get => _pointId ?? string.Empty;
            set => _pointId = value ?? string.Empty;
        }

        public float Radius
        {
            get => _radius;
            set
            {
                if (!IsValidRadius(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"radius must be at least {MinRadius} and finite");
                }

                _radius = value;
                ShowState();
            }
        }

        public GameObject ClosedVisualPrefab
        {
            get => _closedVisualPrefab;
            set => _closedVisualPrefab = value;
        }

        public GameObject OpenVisualPrefab
        {
            get => _openVisualPrefab;
            set => _openVisualPrefab = value;
        }

        public bool IsOpen { get; private set; }

        public static bool IsValidRadius(float radius) => radius >= MinRadius && !float.IsInfinity(radius);

        public static ExtractionZone[] FindAll()
        {
            return FindObjectsByType<ExtractionZone>(FindObjectsInactive.Exclude);
        }

        private void Awake()
        {
            Build();
        }

        public bool Contains(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            var radius = IsValidRadius(_radius) ? _radius : DefaultRadius;
            return offset.sqrMagnitude <= radius * radius;
        }

        public void SetOpen(bool open)
        {
            if (IsOpen == open)
            {
                return;
            }

            IsOpen = open;
            ShowState();
        }

        // Awake でも呼ぶが、Edit Mode で置いたときにシーンへ見た目を残すには置いた側が呼ぶので公開する。
        public void Build()
        {
            ShowState();
        }

        private void ShowState()
        {
            var closed = Visual(VisualName, _closedVisualPrefab);
            var open = Visual(VisualName + "-Open", _openVisualPrefab);

            closed.gameObject.SetActive(!IsOpen);
            open.gameObject.SetActive(IsOpen);

            var diameter = (IsValidRadius(_radius) ? _radius : DefaultRadius) * 2f;
            if (_closedVisualPrefab == null)
            {
                closed.localScale = new Vector3(diameter, PlaceholderThickness, diameter);
                Paint(closed, DefaultClosedColor);
            }

            // 閉じた見た目だけが差し替えでも、開いた地点を見分けられるよう、開いた側が空なら緑の円を出す。
            if (_openVisualPrefab == null)
            {
                open.localScale = new Vector3(diameter, PlaceholderThickness, diameter);
                Paint(open, DefaultOpenColor);
            }
        }

        private Transform Visual(string objectName, GameObject prefab)
        {
            var existing = transform.Find(objectName);
            if (existing != null)
            {
                return existing;
            }

            // 印の上を歩いて通れるよう、見た目の当たり判定は外す。
            if (prefab != null)
            {
                return Shooter.Runtime.VisualPrefab.Instantiate(prefab, transform, objectName, stripColliders: true).transform;
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = objectName;
            visual.transform.SetParent(transform, false);
            // Cylinder は高さ 2 なので、縮めた後の半分の高さは localScale.y と同じになる。
            visual.transform.localPosition = new Vector3(0f, PlaceholderThickness, 0f);
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                // Destroy は次のフレームまで残り、その間に移動の判定に当たるので、すぐ消す。
                DestroyImmediate(collider);
            }

            return visual.transform;
        }

        private static void Paint(Transform visual, Color color)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この印だけの上書きで色を付ける。
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
