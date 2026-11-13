using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// モード 1 つ分のルールをアセットとして持つ。<see cref="ToRules"/> で不変の <see cref="GameRules"/> に変換して使う。
    /// 検証は <see cref="GameRules"/> に任せ、失敗したらどのアセットの値かを添えて <see cref="GameRulesDefinitionException"/> を投げる。
    /// 新しく作ったときの値は <see cref="GameRules.Default"/> と同じ。
    /// 実際に使う値はアセット (Assets/game/Rules/*.asset、Fps.Editor.GameRulesAssetBuilder が作る) の側にある。
    /// </summary>
    [CreateAssetMenu(fileName = "GameRules", menuName = "Fps/Game Rules")]
    public sealed class GameRulesDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("味方に弾が当たったときの扱い")]
        private FriendlyFire _friendlyFire = FriendlyFire.Off;

        [SerializeField, Tooltip("倒されたプレイヤーがいつ戻ってくるか")]
        private RespawnMode _respawnMode = RespawnMode.Individual;

        [SerializeField, Min(0.01f), Tooltip("波と波の間隔 (秒)。Respawn Mode が Wave のときだけ使う")]
        private float _waveIntervalSeconds = GameRules.DefaultWaveIntervalSeconds;

        [Header("自動回復")]
        [SerializeField, Min(0f), Tooltip("最後に被弾してから回復が始まるまでの秒数")]
        private float _regenDelaySeconds;

        [SerializeField, Min(0f), Tooltip("1 秒あたりの回復量。0 なら自動回復しない")]
        private float _regenPerSecond;

        [Header("死亡時")]
        [SerializeField, Tooltip("倒されたプレイヤーの体をどう見せるか")]
        private DeathBody _deathBody = DeathBody.Vanish;

        [SerializeField, Tooltip("倒されてからリスポーンするまで、自分のカメラに何を映すか")]
        private DeathCamera _deathCamera = DeathCamera.None;

        [Header("弾薬")]
        [SerializeField, Tooltip("オンなら予備弾が減らない")]
        private bool _infiniteReserve;

        [SerializeField, Min(0f), Tooltip("初期予備弾の倍率")]
        private float _reserveAmmoMultiplier = 1f;

        public FriendlyFire FriendlyFire
        {
            get => _friendlyFire;
            set => _friendlyFire = value;
        }

        public RespawnMode RespawnMode
        {
            get => _respawnMode;
            set => _respawnMode = value;
        }

        public float WaveIntervalSeconds
        {
            get => _waveIntervalSeconds;
            set => _waveIntervalSeconds = value;
        }

        public float RegenDelaySeconds
        {
            get => _regenDelaySeconds;
            set => _regenDelaySeconds = value;
        }

        public float RegenPerSecond
        {
            get => _regenPerSecond;
            set => _regenPerSecond = value;
        }

        public DeathBody DeathBody
        {
            get => _deathBody;
            set => _deathBody = value;
        }

        public DeathCamera DeathCamera
        {
            get => _deathCamera;
            set => _deathCamera = value;
        }

        public bool InfiniteReserve
        {
            get => _infiniteReserve;
            set => _infiniteReserve = value;
        }

        public float ReserveAmmoMultiplier
        {
            get => _reserveAmmoMultiplier;
            set => _reserveAmmoMultiplier = value;
        }

        /// <summary>定義を不変の <see cref="GameRules"/> に変換する。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="GameRulesDefinitionException">値が不正なとき。メッセージにアセット名を含む。</exception>
        public GameRules ToRules()
        {
            try
            {
                return new GameRules(
                    _friendlyFire,
                    _respawnMode,
                    _waveIntervalSeconds,
                    _regenDelaySeconds,
                    _regenPerSecond,
                    _deathBody,
                    _deathCamera,
                    _infiniteReserve,
                    _reserveAmmoMultiplier);
            }
            catch (ArgumentException e)
            {
                throw new GameRulesDefinitionException($"game rules '{SourceName}' are invalid: {e.Message}", e);
            }
        }

        /// <summary>
        /// <see cref="ToRules"/> と同じ変換を、例外の代わりに戻り値で失敗を知らせて行う。
        /// 成功したら rules に結果を入れ error を null にして true、失敗したら rules を null、error にアセット名を含む理由を入れて false。
        /// </summary>
        public bool TryToRules(out GameRules rules, out string error)
        {
            try
            {
                rules = ToRules();
                error = null;
                return true;
            }
            catch (GameRulesDefinitionException e)
            {
                rules = null;
                error = e.Message;
                return false;
            }
        }

        /// <summary>rules の値をすべて写す。rules は検査済みなので、写した後は必ず <see cref="ToRules"/> に通る。</summary>
        public void CopyFrom(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            _friendlyFire = rules.FriendlyFire;
            _respawnMode = rules.RespawnMode;
            _waveIntervalSeconds = rules.WaveIntervalSeconds;
            _regenDelaySeconds = rules.RegenDelaySeconds;
            _regenPerSecond = rules.RegenPerSecond;
            _deathBody = rules.DeathBody;
            _deathCamera = rules.DeathCamera;
            _infiniteReserve = rules.InfiniteReserve;
            _reserveAmmoMultiplier = rules.ReserveAmmoMultiplier;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed game rules)" : name;
    }

    /// <summary><see cref="GameRulesDefinition"/> の内容が不正で <see cref="GameRules"/> に変換できない。</summary>
    public sealed class GameRulesDefinitionException : Exception
    {
        public GameRulesDefinitionException(string message)
            : base(message)
        {
        }

        public GameRulesDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
