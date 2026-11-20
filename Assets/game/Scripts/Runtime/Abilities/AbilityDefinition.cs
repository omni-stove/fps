using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
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

        public string Id
        {
            get => string.IsNullOrEmpty(_id) ? name : _id;
            set => _id = value ?? string.Empty;
        }

        public int MaxCharges
        {
            get => _maxCharges;
            set => _maxCharges = value;
        }

        public float RechargeSeconds
        {
            get => _rechargeSeconds;
            set => _rechargeSeconds = value;
        }

        public float CooldownSeconds
        {
            get => _cooldownSeconds;
            set => _cooldownSeconds = value;
        }

        public float WindupSeconds
        {
            get => _windupSeconds;
            set => _windupSeconds = value;
        }

        // 名前の欄は種類ごとのアセットが既に持っている (_nameKey) ので、基底では欄を持たずに引き口だけを揃える。
        public virtual string DisplayNameKey => string.Empty;

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

        // 枠に入れるたびに呼ぶので、状態を持つなら毎回新しいインスタンスを返す。
        public abstract IAbility CreateAbility();

        private string SourceName()
        {
            return string.IsNullOrEmpty(name) ? "(unnamed ability)" : name;
        }
    }
}
