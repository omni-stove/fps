using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 入力を自分で受けないのは、拾った物を入れる脱出のセッションを ExtractionRunner だけが持つため。
    // 届く範囲をトリガーにしないのは、Runner が近い箱を毎回選び直すので、出入りを数えなくても足りるため。
    [DisallowMultipleComponent]
    public sealed class LootContainer : MonoBehaviour
    {
        public const string LogTag = LootLog.Tag;

        public const float DefaultReach = 1.6f;
        public const float MinReach = 0.1f;
        public const string VisualName = "Visual";

        public static readonly Vector3 DefaultVisualSize = new Vector3(1f, 0.6f, 0.7f);

        // 弾薬箱 (くすんだ緑) と見分けが付くよう、開ける前は茶色、開けた後は暗い灰色にする。
        public static readonly Color DefaultClosedColor = new Color(0.45f, 0.3f, 0.16f);
        public static readonly Color DefaultOpenedColor = new Color(0.22f, 0.22f, 0.22f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField, Tooltip("箱の Id。シーンの中で重ならないこと。ネットの試合で開ける要求と開けた記録はこれで引く (オブジェクトの名前は使わない)")]
        private string _containerId = string.Empty;

        [SerializeField, Tooltip("中身を引く表の Id (loot.json の tables[].id)。表に無ければ開けても空")]
        private string _tableId = string.Empty;

        [SerializeField, Min(MinReach), Tooltip("この水平の距離 (m) 以内に立てば開けられる")]
        private float _reach = DefaultReach;

        [SerializeField, Tooltip("開ける前の見た目。原点が底面の中心。空なら仮の箱を作る")]
        private GameObject _visualPrefab;

        [SerializeField, Tooltip("開けた後の見た目。空なら開ける前の見た目のまま (仮の箱なら色を変える)")]
        private GameObject _openedVisualPrefab;

        private readonly List<ItemCount> _contents = new List<ItemCount>();

        public string ContainerId
        {
            get => _containerId ?? string.Empty;
            set => _containerId = value ?? string.Empty;
        }

        public string TableId
        {
            get => _tableId ?? string.Empty;
            set => _tableId = value ?? string.Empty;
        }

        public float Reach
        {
            get => _reach;
            set
            {
                if (!IsValidReach(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"reach must be at least {MinReach} and finite");
                }

                _reach = value;
            }
        }

        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        public GameObject OpenedVisualPrefab
        {
            get => _openedVisualPrefab;
            set => _openedVisualPrefab = value;
        }

        public bool IsOpened { get; private set; }

        public IReadOnlyList<ItemCount> Contents => _contents;

        public Vector3 Position => transform.position;

        public static bool IsValidReach(float reach) => reach >= MinReach && !float.IsInfinity(reach);

        public static LootContainer[] FindAll()
        {
            return FindObjectsByType<LootContainer>(FindObjectsInactive.Exclude);
        }

        private void Awake()
        {
            Build();
        }

        public float HorizontalDistanceTo(Vector3 position)
        {
            var offset = position - transform.position;
            offset.y = 0f;
            return offset.magnitude;
        }

        public bool IsInReach(Vector3 position)
        {
            return HorizontalDistanceTo(position) <= (IsValidReach(_reach) ? _reach : DefaultReach);
        }

        // 表が null なら空のまま開ける。表の書き忘れで箱が開かず、同じ所で何度も押させないようにする。
        public IReadOnlyList<ItemCount> Open(LootTable table, System.Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (IsOpened)
            {
                throw new InvalidOperationException($"loot container '{name}' has already been opened");
            }

            IsOpened = true;
            _contents.Clear();
            if (table != null)
            {
                _contents.AddRange(LootRoll.Roll(table, random));
            }

            ShowState();
            return _contents.AsReadOnly();
        }

        // 中身を引いたのが専用サーバーのときに、開いた見た目だけを出す。中身を知らないので Contents は空にする。
        public void ShowOpened()
        {
            if (IsOpened)
            {
                return;
            }

            IsOpened = true;
            _contents.Clear();
            ShowState();
        }

        // Awake でも呼ぶが、Edit Mode で置いたときにシーンへ見た目を残すには置いた側が呼ぶので公開する。
        public void Build()
        {
            ShowState();
        }

        private void ShowState()
        {
            var closed = transform.Find(VisualName);
            var opened = transform.Find(VisualName + "-Opened");
            var useOpenedPrefab = IsOpened && _openedVisualPrefab != null;

            if (closed == null)
            {
                closed = CreateVisual(_visualPrefab, VisualName);
            }

            if (useOpenedPrefab && opened == null)
            {
                opened = CreateVisual(_openedVisualPrefab, VisualName + "-Opened");
            }

            closed.gameObject.SetActive(!useOpenedPrefab);
            if (opened != null)
            {
                opened.gameObject.SetActive(useOpenedPrefab);
            }

            // 色の上書きはシーンに保存されないので、Edit Mode で置いた仮の箱にも組み立てるたびに付け直す。
            if (_visualPrefab == null && !useOpenedPrefab)
            {
                Paint(closed, IsOpened ? DefaultOpenedColor : DefaultClosedColor);
            }
        }

        private Transform CreateVisual(GameObject prefab, string objectName)
        {
            // 見た目の当たり判定を外さないのは、身を隠せる固い物として置くため。
            if (prefab != null)
            {
                return Shooter.Runtime.VisualPrefab.Instantiate(prefab, transform, objectName, stripColliders: false).transform;
            }

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = objectName;
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, DefaultVisualSize.y / 2f, 0f);
            visual.transform.localScale = DefaultVisualSize;
            return visual.transform;
        }

        private static void Paint(Transform visual, Color color)
        {
            var renderer = visual.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            // 共有マテリアルを塗り替えると他のプリミティブまで色が変わるので、この箱だけの上書きで色を付ける。
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
