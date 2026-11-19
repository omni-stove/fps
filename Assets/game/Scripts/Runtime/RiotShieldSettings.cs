using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class RiotShieldSettings
    {
        [SerializeField, Tooltip("盾の武器にする。持っている間は前に盾を出して弾を止め、撃てなくなり、攻撃のボタンで盾で殴る")]
        private bool _enabled;

        [SerializeField, UnityEngine.Range(1f, RiotShieldSpec.MaxBlockArcDegrees), Tooltip("盾の正面を中心に、弾を止める向きの扇の全体の角度 (度)。外から撃たれた弾は止めずに持ち主へ通す")]
        private float _blockArcDegrees = RiotShieldSpec.DefaultBlockArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("見た目が空のときに作る板の幅 (m)")]
        private float _width = RiotShieldSpec.DefaultWidth;

        [SerializeField, Min(0.01f), Tooltip("見た目が空のときに作る板の高さ (m)")]
        private float _height = RiotShieldSpec.DefaultHeight;

        [SerializeField, UnityEngine.Range(0.01f, 1f), Tooltip("盾を構えている間に移動の速さへ掛ける倍率。走って盾を下げている間は掛けない")]
        private float _moveSpeedMultiplier = RiotShieldSpec.DefaultMoveSpeedMultiplier;

        [SerializeField, Min(1), Tooltip("盾で殴る一撃のダメージ")]
        private int _bashDamage = RiotShieldSpec.DefaultBashDamage;

        [SerializeField, Min(0.01f), Tooltip("殴りの届く距離 (m)")]
        private float _bashReach = RiotShieldSpec.DefaultBashReach;

        [SerializeField, UnityEngine.Range(1f, MeleeSpec.MaxArcDegrees), Tooltip("殴りの前方の扇の全体の角度 (度)")]
        private float _bashArcDegrees = RiotShieldSpec.DefaultBashArcDegrees;

        [SerializeField, Min(0.01f), Tooltip("殴りの振りの秒数")]
        private float _bashSwingSeconds = RiotShieldSpec.DefaultBashSwingSeconds;

        [SerializeField, Min(0f), Tooltip("振り終えてから次に殴れるまでの秒数")]
        private float _bashCooldownSeconds = RiotShieldSpec.DefaultBashCooldownSeconds;

        [SerializeField, Min(0f), Tooltip("振り始めから当たりを判定するまでの秒数。振りの秒数以下")]
        private float _bashImpactSeconds = RiotShieldSpec.DefaultBashImpactSeconds;

        [SerializeField, Min(0f), Tooltip("殴るときに前へ踏み込む距離 (m)")]
        private float _bashLungeDistance = RiotShieldSpec.DefaultBashLungeDistance;

        [SerializeField, Tooltip("持っている間に出す盾の見た目と当たり判定。持ち主の原点 (体の中心) から見た位置で、前が +Z。、根にコライダーが要る。空なら幅と高さの板を作る")]
        private GameObject _prefab;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float BlockArcDegrees { get => _blockArcDegrees; set => _blockArcDegrees = value; }

        public float Width { get => _width; set => _width = value; }

        public float Height { get => _height; set => _height = value; }

        public float MoveSpeedMultiplier { get => _moveSpeedMultiplier; set => _moveSpeedMultiplier = value; }

        public int BashDamage { get => _bashDamage; set => _bashDamage = value; }

        public float BashReach { get => _bashReach; set => _bashReach = value; }

        public float BashArcDegrees { get => _bashArcDegrees; set => _bashArcDegrees = value; }

        public float BashSwingSeconds { get => _bashSwingSeconds; set => _bashSwingSeconds = value; }

        public float BashCooldownSeconds { get => _bashCooldownSeconds; set => _bashCooldownSeconds = value; }

        public float BashImpactSeconds { get => _bashImpactSeconds; set => _bashImpactSeconds = value; }

        public float BashLungeDistance { get => _bashLungeDistance; set => _bashLungeDistance = value; }

        public GameObject Prefab { get => _prefab; set => _prefab = value; }

        public RiotShieldSpec ToSpec()
        {
            if (!_enabled)
            {
                return null;
            }

            var bash = new MeleeSpec(
                _bashDamage, _bashReach, _bashArcDegrees, _bashSwingSeconds, _bashCooldownSeconds, _bashImpactSeconds, _bashLungeDistance);
            return new RiotShieldSpec(_blockArcDegrees, _width, _height, _moveSpeedMultiplier, bash);
        }

        // 見た目 (Prefab) は spec に無いので、入っているものを残す。
        public void Set(RiotShieldSpec spec)
        {
            _enabled = spec != null;
            if (spec == null)
            {
                return;
            }

            _blockArcDegrees = spec.BlockArcDegrees;
            _width = spec.Width;
            _height = spec.Height;
            _moveSpeedMultiplier = spec.MoveSpeedMultiplier;
            _bashDamage = spec.Bash.Damage;
            _bashReach = spec.Bash.Reach;
            _bashArcDegrees = spec.Bash.ArcDegrees;
            _bashSwingSeconds = spec.Bash.SwingSeconds;
            _bashCooldownSeconds = spec.Bash.CooldownSeconds;
            _bashImpactSeconds = spec.Bash.ImpactSeconds;
            _bashLungeDistance = spec.Bash.LungeDistance;
        }
    }
}
