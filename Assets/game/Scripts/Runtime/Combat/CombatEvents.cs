using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 戦闘で起きたことを演出と UI (<see cref="WeaponPresentation"/>・<see cref="CharacterAudio"/>・<see cref="HitMarkerView"/> など) に知らせる、
    /// 静的なイベントの集まり。撃つ側・撃たれる側は購読者を知らずに Raise 系を呼ぶだけにし、演出の有無で戦闘の結果が変わらないようにする。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><see cref="ShotFired"/>: 1 発撃つたび。オフラインでは <see cref="WeaponController.Fire"/> (プレイヤー) と <see cref="Hitscan.Fire"/> (敵など)。</item>
    /// <item><see cref="Impact"/>: 撃った弾が何か (壁・人) に当たるたび。呼ぶ所は <see cref="ShotFired"/> と同じ。</item>
    /// <item><see cref="HitConfirmed"/>: 撃った本人のクライアントでだけ、生きている相手に当てたとき。オフラインでは <see cref="WeaponController.Fire"/>。</item>
    /// <item><see cref="DamageTaken"/>: 撃たれた本人のクライアントでだけ。オフラインでは <see cref="Damageable.IsLocalPlayer"/> の <see cref="Damageable"/>。</item>
    /// <item><see cref="Killed"/>: 倒れるたび。オフラインでは <see cref="Damageable"/> が倒れたときに、<see cref="Attribute"/> で決めた倒した者と武器で出す。</item>
    /// </list>
    /// 購読者が例外を投げても戦闘の処理は止めず、例外をログに出して残りの購読者を呼ぶ。
    /// 静的なので、ドメインの再読み込みを切った Play でも前回の購読が残らないよう、Play の開始時に購読をすべて外す。
    /// </remarks>
    public static class CombatEvents
    {
        // 入れ子の Attribute を戻せるよう、今の帰属を 1 つだけ持ち、スコープが前の値を覚える。
        private static ShotAttribution? _attribution;

        /// <summary>1 発撃ったとき。</summary>
        public static event Action<ShotFiredInfo> ShotFired;

        /// <summary>撃った弾が何かに当たったとき。</summary>
        public static event Action<ImpactInfo> Impact;

        /// <summary>撃った本人のクライアントで、生きている相手に当てたとき。</summary>
        public static event Action<HitConfirm> HitConfirmed;

        /// <summary>撃たれた本人のクライアントで、生きている間にダメージを受けたとき。</summary>
        public static event Action<DamageTakenInfo> DamageTaken;

        /// <summary>誰かが倒れたとき。キルフィードの 1 行を渡す。</summary>
        public static event Action<KillFeedEntry> Killed;

        /// <summary>
        /// 今の帰属 (<see cref="Attribute"/> の中なら倒した者と武器)。外なら null。
        /// </summary>
        public static ShotAttribution? CurrentAttribution => _attribution;

        /// <summary>
        /// オフラインでキルフィードに使う Id。キャラの本体 (撃つ側は撃った者、倒れる側は <see cref="Damageable"/> の GameObject) の InstanceID。
        /// null なら 0。ネット対戦の Id とは別物で、同じ Play の間だけ一意。
        /// </summary>
        /// <remarks>
        /// ルートを辿らないのは、敵や標的が 1 つの親の下にまとめて置かれ、ルートでは全員が同じ Id になるため。
        /// </remarks>
        public static int OfflineId(GameObject gameObject)
        {
            return gameObject != null ? gameObject.GetInstanceID() : 0;
        }

        /// <summary>
        /// 戻り値を Dispose するまでの間に倒れた <see cref="Damageable"/> を、killer が weaponId で倒したことにする。
        /// 撃つ側が命中の処理の前後をこれで囲む。入れ子にでき、Dispose で前の帰属に戻る。
        /// </summary>
        /// <param name="killer">倒した者。null なら帰属を外す (倒れた本人の自滅として出す)。</param>
        /// <param name="weaponId">武器の Id。null は空文字として扱う。</param>
        public static AttributionScope Attribute(GameObject killer, string weaponId)
        {
            var previous = _attribution;
            _attribution = killer != null ? new ShotAttribution(OfflineId(killer), weaponId) : default(ShotAttribution?);
            return new AttributionScope(previous);
        }

        public static void RaiseShotFired(GameObject shooter, Vector3 origin, Vector3 direction, string weaponId)
        {
            Invoke(ShotFired, new ShotFiredInfo(shooter, origin, direction, weaponId));
        }

        public static void RaiseImpact(Vector3 point, Vector3 normal, Collider collider)
        {
            Invoke(Impact, new ImpactInfo(point, normal, collider));
        }

        public static void RaiseHitConfirmed(HitConfirm confirm)
        {
            Invoke(HitConfirmed, confirm);
        }

        public static void RaiseDamageTaken(Vector3? sourcePosition, int amount)
        {
            Invoke(DamageTaken, new DamageTakenInfo(sourcePosition, amount));
        }

        public static void RaiseKilled(KillFeedEntry entry)
        {
            Invoke(Killed, entry);
        }

        /// <summary>購読をすべて外し、帰属も消す。Play の開始時と、テストの後片付けで呼ぶ。</summary>
        public static void ClearSubscribers()
        {
            ShotFired = null;
            Impact = null;
            HitConfirmed = null;
            DamageTaken = null;
            Killed = null;
            _attribution = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            ClearSubscribers();
        }

        private static void Invoke<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(value);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        /// <summary><see cref="Attribute"/> の戻り値。Dispose で前の帰属に戻す。</summary>
        public readonly struct AttributionScope : IDisposable
        {
            private readonly ShotAttribution? _previous;

            internal AttributionScope(ShotAttribution? previous)
            {
                _previous = previous;
            }

            public void Dispose()
            {
                _attribution = _previous;
            }
        }
    }

    /// <summary>倒れたときに、誰がどの武器で倒したことにするか (<see cref="CombatEvents.Attribute"/>)。</summary>
    public readonly struct ShotAttribution
    {
        // default では null になるので、読むときに空文字へ寄せる。
        private readonly string _weaponId;

        /// <summary>倒した者の Id (<see cref="CombatEvents.OfflineId"/>)。</summary>
        public int KillerId { get; }

        /// <summary>武器の Id。null にならない。</summary>
        public string WeaponId => _weaponId ?? string.Empty;

        public ShotAttribution(int killerId, string weaponId)
        {
            KillerId = killerId;
            _weaponId = weaponId ?? string.Empty;
        }
    }

    /// <summary><see cref="CombatEvents.ShotFired"/> で渡す、1 発の情報。</summary>
    public readonly struct ShotFiredInfo
    {
        private readonly string _weaponId;

        /// <summary>撃った者の本体 (プレイヤーは照準のルート、敵は <see cref="EnemyAgent"/> の GameObject)。分からなければ null。</summary>
        public GameObject Shooter { get; }

        /// <summary>Raycast の始点。</summary>
        public Vector3 Origin { get; }

        /// <summary>撃った向き (単位ベクトル)。</summary>
        public Vector3 Direction { get; }

        /// <summary>武器の <see cref="WeaponSpec.Id"/>。分からなければ空文字。null にならない。</summary>
        public string WeaponId => _weaponId ?? string.Empty;

        public ShotFiredInfo(GameObject shooter, Vector3 origin, Vector3 direction, string weaponId)
        {
            Shooter = shooter;
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : direction;
            _weaponId = weaponId ?? string.Empty;
        }
    }

    /// <summary><see cref="CombatEvents.Impact"/> で渡す、弾が当たった所。</summary>
    public readonly struct ImpactInfo
    {
        /// <summary>当たった点。</summary>
        public Vector3 Point { get; }

        /// <summary>当たった面の法線。</summary>
        public Vector3 Normal { get; }

        /// <summary>当たったコライダー。</summary>
        public Collider Collider { get; }

        public ImpactInfo(Vector3 point, Vector3 normal, Collider collider)
        {
            Point = point;
            Normal = normal;
            Collider = collider;
        }
    }

    /// <summary><see cref="CombatEvents.DamageTaken"/> で渡す、撃たれた本人への被弾の情報。</summary>
    public readonly struct DamageTakenInfo
    {
        /// <summary>攻撃した者の位置。分からなければ null (方向は出せない)。</summary>
        public Vector3? SourcePosition { get; }

        /// <summary>受けたダメージ量。</summary>
        public int Amount { get; }

        public DamageTakenInfo(Vector3? sourcePosition, int amount)
        {
            SourcePosition = sourcePosition;
            Amount = amount;
        }
    }
}
