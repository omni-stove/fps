using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 1 tick 分の敵の決定。移動・向き・射撃の実行は呼び出し側が行う。
    /// </summary>
    public readonly struct EnemyDecision
    {
        /// <summary>何もしない決定。</summary>
        public static EnemyDecision None => default;

        /// <summary>向かう位置。null ならその場に留まる。</summary>
        public Vector3? MoveTo { get; }

        /// <summary>向く位置。null なら向きを指定しない。</summary>
        public Vector3? LookAt { get; }

        /// <summary>この tick に撃つか。</summary>
        public bool Fire { get; }

        /// <summary>この tick にしゃがむか。</summary>
        public bool Crouch { get; }

        public EnemyDecision(Vector3? moveTo, Vector3? lookAt, bool fire)
            : this(moveTo, lookAt, fire, false)
        {
        }

        public EnemyDecision(Vector3? moveTo, Vector3? lookAt, bool fire, bool crouch)
        {
            MoveTo = moveTo;
            LookAt = lookAt;
            Fire = fire;
            Crouch = crouch;
        }
    }
}
