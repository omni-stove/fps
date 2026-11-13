using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="Damageable"/> の体力を <see cref="GameRules"/> の自動回復 (<see cref="GameRules.RegenDelaySeconds"/> /
    /// <see cref="GameRules.RegenPerSecond"/>) で戻す。判断は <see cref="HealthRegenerator"/> に任せ、ここは Unity との繋ぎだけを持つ。
    /// <list type="bullet">
    /// <item><see cref="Damageable.Damaged"/> で <see cref="HealthRegenerator.NotifyDamaged"/> を呼び、待ち時間を戻す。</item>
    /// <item>Update で <see cref="Tick"/> を呼ぶ。Edit Mode のテストでは直接呼ぶ。</item>
    /// <item><see cref="Damageable.ResetHealth"/> などで Health が作り直されたら (<see cref="Damageable.HealthReset"/>)、新しい Health に付け直す。
    /// 付け直した直後は待ち時間なしで回復を始める (作り直しは被弾ではないため)。</item>
    /// </list>
    /// プレイヤーにだけ付ける (敵の体力は回復させない)。<see cref="Bind"/> するまでは何もしない。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthRegen : MonoBehaviour
    {
        private Damageable _target;
        private GameRules _rules;
        private HealthRegenerator _regenerator;

        /// <summary>回復させる相手。<see cref="Bind"/> 前は null。</summary>
        public Damageable Target => _target;

        /// <summary>回復の速さを決めるルール。<see cref="Bind"/> 前は null。</summary>
        public GameRules Rules => _rules;

        /// <summary>今の Health に付けた回復の計算。<see cref="Bind"/> 前は null。Health が作り直されると別のインスタンスになる。</summary>
        public HealthRegenerator Regenerator => _regenerator;

        public bool IsBound => _target != null && _regenerator != null;

        private void Update()
        {
            if (IsBound)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        /// <summary>
        /// target の体力を rules で回復させる。前に付けていた相手からは外す。同じ相手に付け直したとき (ルールの差し替え) も、
        /// 回復の計算は作り直す (待ち時間と端数は捨てる)。
        /// </summary>
        public void Bind(Damageable target, GameRules rules)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            Unbind();
            _target = target;
            _rules = rules;
            _target.Damaged += OnDamaged;
            _target.HealthReset += OnHealthReset;
            _regenerator = new HealthRegenerator(_target.Health, _rules);
        }

        /// <summary>相手の購読を外し、回復をやめる。付けていなければ何もしない。破棄時にも呼ばれる。</summary>
        public void Unbind()
        {
            if (_target != null)
            {
                _target.Damaged -= OnDamaged;
                _target.HealthReset -= OnHealthReset;
            }

            _target = null;
            _rules = null;
            _regenerator = null;
        }

        /// <summary>時間を deltaSeconds 進め、回復した量を返す。付けていなければ 0。</summary>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            return _regenerator != null ? _regenerator.Tick(deltaSeconds) : 0;
        }

        private void OnDamaged(Damageable _, DamageReport report)
        {
            _regenerator?.NotifyDamaged();
        }

        private void OnHealthReset(Health health)
        {
            _regenerator = new HealthRegenerator(health, _rules);
        }
    }
}
