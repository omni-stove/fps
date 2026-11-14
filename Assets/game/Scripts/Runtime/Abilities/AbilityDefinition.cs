using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 能力 1 つの定義。回数と時間 (<see cref="ToSpec"/> で <see cref="AbilitySpec"/> に) と、振る舞い (<see cref="CreateAbility"/>) を持つ。
    /// 能力の種類ごとに派生クラスを作り、アセットにしてロードアウト (<see cref="LoadoutDefinition.Abilities"/>) や
    /// <see cref="GameBootstrap.AbilityDefinitions"/> に入れる。
    /// </summary>
    public abstract class AbilityDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("ログと保存で使う識別子。空ならアセットの名前")]
        private string _id = string.Empty;

        [SerializeField, Min(1), Tooltip("続けて使える回数の上限")]
        private int _maxCharges = AbilitySpec.DefaultMaxCharges;

        [SerializeField, Min(0f), Tooltip("回数を 1 つ戻すのにかかる秒数。0 なら戻らない")]
        private float _rechargeSeconds = AbilitySpec.DefaultRechargeSeconds;

        [SerializeField, Min(0f), Tooltip("使い始めてから次に使えるまでの秒数")]
        private float _cooldownSeconds = AbilitySpec.DefaultCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("使い始めてから効果が出るまでの秒数")]
        private float _windupSeconds = AbilitySpec.DefaultWindupSeconds;

        /// <summary>ログと保存で使う識別子。未設定ならアセットの名前。</summary>
        public string Id
        {
            get => string.IsNullOrEmpty(_id) ? name : _id;
            set => _id = value ?? string.Empty;
        }

        /// <summary>続けて使える回数の上限。<see cref="ToSpec"/> が 1 以上かを確かめる。</summary>
        public int MaxCharges
        {
            get => _maxCharges;
            set => _maxCharges = value;
        }

        /// <summary>回数を 1 つ戻すのにかかる秒数。0 なら戻らない。<see cref="ToSpec"/> が 0 以上の有限値かを確かめる。</summary>
        public float RechargeSeconds
        {
            get => _rechargeSeconds;
            set => _rechargeSeconds = value;
        }

        /// <summary>使い始めてから次に使えるまでの秒数。<see cref="ToSpec"/> が 0 以上の有限値かを確かめる。</summary>
        public float CooldownSeconds
        {
            get => _cooldownSeconds;
            set => _cooldownSeconds = value;
        }

        /// <summary>使い始めてから効果が出るまでの秒数。<see cref="ToSpec"/> が 0 以上の有限値かを確かめる。</summary>
        public float WindupSeconds
        {
            get => _windupSeconds;
            set => _windupSeconds = value;
        }

        /// <summary>回数と時間を <see cref="AbilitySpec"/> にする。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="AbilityDefinitionException">値が不正。どのアセットかを添える。</exception>
        public AbilitySpec ToSpec()
        {
            try
            {
                return new AbilitySpec(_maxCharges, _rechargeSeconds, _cooldownSeconds, _windupSeconds);
            }
            catch (ArgumentException e)
            {
                throw new AbilityDefinitionException($"ability '{SourceName()}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>
        /// 枠 1 つ分の振る舞いを作る。枠に入れるたびに呼ぶので、状態を持つなら毎回新しいインスタンスを返す。null は返さない。
        /// </summary>
        public abstract IAbility CreateAbility();

        private string SourceName()
        {
            return string.IsNullOrEmpty(name) ? "(unnamed ability)" : name;
        }
    }
}
