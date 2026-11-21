using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 中身を引くのも見た目を作るのも LootContainer に任せ、ここでは空から落とすことと、引いた物を地面に置くことだけをする。
    // 落ちる動きを物理に任せないのは、着く時刻を落とす高さと速さで決め、どの端末でも同じ所に着かせるため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LootContainer))]
    public sealed class SupplyCrate : MonoBehaviour
    {
        public const string ObjectNamePrefix = "SupplyCrate-";

        // 箱の見た目 (既定で幅 1m) と重ならず、拾える範囲 (Pickup.DefaultRadius) が箱の向こうまで届かない距離にする。
        public const float SpreadRadius = 1.5f;

        private LootContainer _container;
        private float _groundHeight;
        private float _fallSpeed = SupplyDropSpec.DefaultFallSpeed;

        public event Action<SupplyCrate> Landed;

        public int Number { get; private set; }

        public bool HasLanded { get; private set; }

        public bool IsOpened => Container.IsOpened;

        public float GroundHeight => _groundHeight;

        public LootContainer Container => _container != null ? _container : _container = GetComponent<LootContainer>();

        public Vector3 Position => transform.position;

        public SupplyDropMark Mark => new SupplyDropMark(Number, Position.ToNumerics(), HasLanded);

        public static SupplyCrate Create(
            int number,
            Vector3 from,
            float groundHeight,
            float fallSpeed,
            float reach,
            GameObject visualPrefab,
            GameObject openedVisualPrefab,
            Transform parent = null)
        {
            if (!(fallSpeed > 0f) || float.IsInfinity(fallSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(fallSpeed), fallSpeed, "fallSpeed must be positive and finite");
            }

            if (float.IsNaN(groundHeight) || float.IsInfinity(groundHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(groundHeight), groundHeight, "groundHeight must be finite");
            }

            var go = new GameObject(ObjectNamePrefix + number);
            try
            {
                // LootContainer は Awake で見た目を作るので、差し替えの見た目を渡し終えるまで無効にしておく。
                go.SetActive(false);
                if (parent != null)
                {
                    go.transform.SetParent(parent, false);
                }

                go.transform.SetPositionAndRotation(new Vector3(from.x, Mathf.Max(from.y, groundHeight), from.z), Quaternion.identity);
                var container = go.AddComponent<LootContainer>();
                container.VisualPrefab = visualPrefab;
                container.OpenedVisualPrefab = openedVisualPrefab;
                container.Reach = reach;
                var crate = go.AddComponent<SupplyCrate>();
                crate._container = container;
                crate.Number = number;
                crate._groundHeight = groundHeight;
                crate._fallSpeed = fallSpeed;
                go.SetActive(true);

                // Edit Mode では Awake が走らず見た目ができないので、ここでも組む (組み済みなら作り直さない)。
                container.Build();
                return crate;
            }
            catch
            {
                DestroySafely(go);
                throw;
            }
        }

        // 呼ぶのは SupplyDropper だけにする。箱が自分の Update で落ちると、止めたモードの箱まで落ち続けるため。
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (HasLanded)
            {
                return;
            }

            var position = transform.position;
            position.y = Mathf.Max(_groundHeight, position.y - (_fallSpeed * deltaSeconds));
            transform.position = position;
            if (position.y <= _groundHeight)
            {
                HasLanded = true;
                Debug.Log(SupplyDropLog.Landed(Number, position.ToNumerics()), this);
                Landed?.Invoke(this);
            }
        }

        public bool CanOpenFrom(Vector3 position)
        {
            return HasLanded && !IsOpened && Container.IsInReach(position);
        }

        // 表が null でも開ける (LootContainer と同じ)。決まった中身は表の物の後に並べて置く。
        public IReadOnlyList<Pickup> Open(int openerId, LootTable table, System.Random random, IReadOnlyList<PickupContents> fixedContents)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (!HasLanded)
            {
                throw new InvalidOperationException($"supply crate '{name}' has not landed yet");
            }

            var items = Container.Open(table, random);
            var contents = new List<PickupContents>(items.Count + (fixedContents?.Count ?? 0));
            foreach (var item in items)
            {
                contents.Add(PickupContents.ForItem(item.ItemId, item.Count));
            }

            if (fixedContents != null)
            {
                foreach (var extra in fixedContents)
                {
                    if (extra != null)
                    {
                        contents.Add(extra);
                    }
                }
            }

            var pickups = new List<Pickup>(contents.Count);
            for (var i = 0; i < contents.Count; i++)
            {
                pickups.Add(Pickup.Create(contents[i], SpreadPosition(i, contents.Count)));
            }

            Debug.Log(SupplyDropLog.Opened(Number, openerId, items, contents.Count - items.Count), this);
            return pickups;
        }

        public Vector3 SpreadPosition(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be in [0, count)");
            }

            var angle = index * Mathf.PI * 2f / count;
            var center = transform.position;
            return new Vector3(center.x + (Mathf.Cos(angle) * SpreadRadius), _groundHeight, center.z + (Mathf.Sin(angle) * SpreadRadius));
        }

        private static void DestroySafely(GameObject target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
