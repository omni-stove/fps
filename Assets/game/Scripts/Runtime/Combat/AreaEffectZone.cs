using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 地面に残る範囲の効果 (スモーク・炎など) 1 つ。中心・半径・寿命を持ち、寿命が来たら消える。
    /// オフラインは <see cref="Spread"/> で効果ごと置き、ネット対戦のクライアントはサーバーから届いた範囲を <see cref="Show"/> で見た目だけ出す。
    /// 種類ごとの効き目は <c>AreaEffectZone.Smoke.cs</c>・<c>AreaEffectZone.Fire.cs</c> の partial が受け持つ。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class AreaEffectZone : MonoBehaviour
    {
        public const string LogTag = "[AreaEffect]";

        public const string ObjectName = "AreaEffect";

        /// <summary>見た目を置く子の名前。</summary>
        public const string VisualName = "Visual";

        /// <summary>種類 (<see cref="AreaEffectSpec.VisualId"/>) と同じ名前の見た目のプレハブを置く Resources のフォルダー。</summary>
        public const string VisualResourceFolder = "Presentation/AreaEffects";

        private float _remaining;
        private bool _ended;

        /// <summary>消えたときに 1 度だけ呼ばれる。</summary>
        public event Action<AreaEffectZone> Ended;

        /// <summary>見た目を選ぶ種類 (<see cref="AreaEffectSpec.VisualId"/>)。空なら仮の形。</summary>
        public string KindId { get; private set; } = string.Empty;

        public Vector3 Center => transform.position;

        public float Radius { get; private set; }

        public float RemainingSeconds => _ended ? 0f : Mathf.Max(0f, _remaining);

        public bool HasEnded => _ended;

        /// <summary>効き目の値。見た目だけを出しているネット対戦のクライアントでは null。</summary>
        public AreaEffectSpec Spec { get; private set; }

        /// <summary>置いた者の本体。キルフィードの帰属に使う。居なければ null。</summary>
        public GameObject Owner { get; private set; }

        public string WeaponId { get; private set; } = string.Empty;

        /// <summary>spec の範囲を center に置き、寿命が来るまで効かせる。</summary>
        public static AreaEffectZone Spread(AreaEffectSpec spec, Vector3 center, GameObject owner, string weaponId)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var zone = Create(spec.VisualId, spec, center, spec.Radius, spec.DurationSeconds);
            zone.Owner = owner;
            zone.WeaponId = weaponId ?? string.Empty;
            zone.StartSmoke();
            return zone;
        }

        /// <summary>サーバーが効かせている範囲の見た目だけを出す。</summary>
        public static AreaEffectZone Show(string kindId, Vector3 center, float radius, float remainingSeconds)
        {
            return Create(kindId, null, center, radius, remainingSeconds);
        }

        private static AreaEffectZone Create(string kindId, AreaEffectSpec spec, Vector3 center, float radius, float seconds)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a non-negative finite number");
            }

            var go = new GameObject(ObjectName);
            go.transform.position = center;
            var zone = go.AddComponent<AreaEffectZone>();
            zone.KindId = kindId ?? string.Empty;
            zone.Spec = spec;
            zone.Radius = radius;
            zone._remaining = seconds;
            zone.BuildVisual();
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} spread kind={1} at=({2:0.0}, {3:0.0}, {4:0.0}) radius={5:0.0} seconds={6:0.0}",
                LogTag,
                zone.KindId,
                center.x,
                center.y,
                center.z,
                radius,
                seconds));
            return zone;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            StopSmoke();
        }

        /// <summary>寿命を deltaSeconds 進め、効き目を出し、寿命が来たら消す。消えたら true。</summary>
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_ended)
            {
                return false;
            }

            if (Spec != null)
            {
                var step = Mathf.Min(deltaSeconds, _remaining);
                StepSmoke(step);
                StepFire(step);
            }

            _remaining -= deltaSeconds;
            if (_remaining > 0f)
            {
                return false;
            }

            End();
            return true;
        }

        /// <summary>ネット対戦でサーバーから届いた残りの秒数に合わせる。0 なら消す。</summary>
        public void SetRemaining(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a non-negative finite number");
            }

            if (seconds <= 0f)
            {
                End();
                return;
            }

            _remaining = seconds;
        }

        /// <summary>寿命の前でも消す。2 回目以降は何もしない。</summary>
        public void End()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            StopSmoke();
            Debug.Log($"{LogTag} ended kind={KindId}");
            try
            {
                Ended?.Invoke(this);
            }
            finally
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }

        partial void StartSmoke();

        partial void StopSmoke();

        partial void BuildSmokeVisual(ref bool built);

        partial void StepSmoke(float deltaSeconds);

        partial void StepFire(float deltaSeconds);

        private void BuildVisual()
        {
            var prefab = KindId.Length > 0 ? Resources.Load<GameObject>(VisualResourceFolder + "/" + KindId) : null;
            if (prefab != null)
            {
                VisualPrefab.Instantiate(prefab, transform, VisualName, stripColliders: true);
                return;
            }

            var built = false;
            BuildSmokeVisual(ref built);
            if (built)
            {
                return;
            }

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = VisualName;

            // 見た目だけの球が弾・視線・移動を遮らないよう、すぐ当たり判定を外す
            DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * (Radius * 2f);
        }
    }
}
