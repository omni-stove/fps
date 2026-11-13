using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ガンゲーム (<see cref="GunGame"/>) を進める <see cref="ModeRunnerBase"/>。全員が別の陣営 (<see cref="FreeForAll"/>) で、
    /// <see cref="Ladder"/> の武器の並びを 0 段目から順に持ち、最後の段の武器で倒した人が勝つ。倒れた人は <see cref="RespawnSeconds"/> 後に戻る。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>始めたとき、全員の持ち武器を今の段の武器 1 本だけにする。プレイヤーは <see cref="WeaponController"/> を作り直し (持ち替えの入力は外す)、
    /// bot は <see cref="EnemyAgent.EquipWeapon"/> で替える。予備弾はプレイヤーの WeaponController のルール (GameBootstrap が配線したモードのルール) に従う。</item>
    /// <item>倒した武器 (<see cref="CombatEvents.CurrentAttribution"/>) を <see cref="GunGame.ReportKill"/> に渡して段を動かし、
    /// 段が変わった人の武器は次の <see cref="ModeRunnerBase.Tick"/> の頭で替える。撃っている最中の WeaponController・EnemyAgent の武器を差し替えないため。</item>
    /// <item>最後の段の武器で倒した人が出たら、次の Tick でその人の陣営の勝ちで終わる (<see cref="GunGame.CheckStanding"/>)。時間切れは段のいちばん進んだ人の勝ち。</item>
    /// <item>陣営の得点は 1 キル 1 点 (<see cref="KillScore"/>) で、勝敗には使わない。HUD には得点の下に自分の段と今の武器、次の武器を出す。</item>
    /// <item>段が動くたびに <c>[Mode] gungame promote|demote|finish</c>、武器を替えるたびに <c>[Mode] gungame equip</c> のログ (<see cref="GunGameText"/>) を出す。</item>
    /// </list>
    /// </remarks>
    public sealed class GunGameRunner : ModeRunnerBase
    {
        public const float DefaultTimeLimitSeconds = 600f;
        public const float DefaultRespawnSeconds = 2f;

        [SerializeField, Tooltip("武器の並び。シーン生成スクリプトが配線する")]
        private GunGameLadderDefinition _ladder;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)。切れたら段のいちばん進んだ人の勝ち")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        private readonly List<SideMember> _pendingEquips = new List<SideMember>();
        private readonly HashSet<SideMember> _warnedUnarmed = new HashSet<SideMember>();
        private readonly List<WeaponDefinition> _tierWeapons = new List<WeaponDefinition>();
        private GunGame _gunGame;
        private ModeSession _gunGameSession;
        private GameRules _weaponRules = GameRules.Default;

        /// <summary>武器の並び。<see cref="CreateGunGameSession"/> (開始時) に読む。</summary>
        public GunGameLadderDefinition Ladder
        {
            get => _ladder;
            set => _ladder = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        /// <summary>進めているガンゲーム。<see cref="CreateGunGameSession"/> で作ったモードを始めていなければ null。</summary>
        public GunGame GunGame => _gunGame != null && Session != null && Session == _gunGameSession ? _gunGame : null;

        /// <summary>
        /// 全員が別の陣営・1 キル 1 点・倒れてから respawnSeconds 後に戻る・gunGame の段で勝敗を決めるモードを作る。
        /// 値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public static ModeSession CreateSession(GunGame gunGame, float timeLimitSeconds, float respawnSeconds)
        {
            if (gunGame == null)
            {
                throw new ArgumentNullException(nameof(gunGame));
            }

            return new ModeSession(
                new FreeForAll(),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore() },
                new IndividualRespawn(respawnSeconds),
                new IWinCondition[] { gunGame });
        }

        /// <summary>
        /// <see cref="Ladder"/> からガンゲームとモードを作り、この Runner が進めるものとして覚えて返す。<see cref="ModeRunnerBase.Begin"/> に渡す
        /// (Play 中は Start が呼ぶ)。Ladder が無いか不正なら <see cref="InvalidOperationException"/>、時間の値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public ModeSession CreateGunGameSession()
        {
            if (_ladder == null)
            {
                throw new InvalidOperationException(
                    $"GunGameRunner '{name}' has no GunGameLadderDefinition; the scene builder (Fps.Editor.GunGameSceneBuilder) wires Ladder");
            }

            GunGameLadder ladder;
            try
            {
                ladder = _ladder.ToLadder();
            }
            catch (WeaponDefinitionException e)
            {
                throw new InvalidOperationException($"gun game ladder '{_ladder.name}' is invalid: {e.Message}", e);
            }

            var gunGame = new GunGame(ladder);
            var session = CreateSession(gunGame, _timeLimitSeconds, _respawnSeconds);

            _gunGame = gunGame;
            _gunGameSession = session;
            _tierWeapons.Clear();
            _tierWeapons.AddRange(_ladder.Tiers);
            return session;
        }

        protected override ModeSession CreateSession()
        {
            return CreateGunGameSession();
        }

        protected override void OnBegan()
        {
            var gunGame = GunGame;
            if (gunGame == null)
            {
                Debug.LogError(
                    $"{LogTag} GunGameRunner '{name}' に CreateGunGameSession で作っていないモードが渡されたため、武器の段を扱いません。",
                    this);
                return;
            }

            _weaponRules = ResolveWeaponRules();
            _pendingEquips.Clear();
            foreach (var member in Members)
            {
                Equip(gunGame, member);
            }
        }

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            var gunGame = GunGame;
            if (gunGame == null || killer == null || victim == null)
            {
                return;
            }

            var attribution = CombatEvents.CurrentAttribution;
            var weaponId = attribution.HasValue ? attribution.Value.WeaponId : string.Empty;
            var kill = gunGame.ReportKill(Session, IdOf(killer), IdOf(victim), weaponId);
            var count = gunGame.Ladder.TierCount;

            if (kill.Demoted)
            {
                Debug.Log(GunGameText.Demote(kill.VictimId, kill.VictimTier, count, gunGame.GetWeapon(kill.VictimId).DisplayName), victim);
                QueueEquip(victim);
            }

            if (kill.Finished)
            {
                Debug.Log(GunGameText.Finish(kill.KillerId, gunGame.GetWeapon(kill.KillerId).DisplayName), killer);
            }
            else if (kill.Promoted)
            {
                Debug.Log(GunGameText.Promote(kill.KillerId, kill.KillerTier, count, gunGame.GetWeapon(kill.KillerId).DisplayName), killer);
                QueueEquip(killer);
            }
        }

        protected override void OnBeforeTick(float deltaSeconds)
        {
            var gunGame = GunGame;
            if (gunGame == null || _pendingEquips.Count == 0)
            {
                return;
            }

            // 終わった後は全員止まっているので、持ち替えても使われない。
            if (Session.State == GameModeState.Playing)
            {
                foreach (var member in _pendingEquips)
                {
                    Equip(gunGame, member);
                }
            }

            _pendingEquips.Clear();
        }

        protected override void OnEnded(Side? winner)
        {
            _pendingEquips.Clear();
        }

        protected override void AppendHudLines(List<string> lines)
        {
            var gunGame = GunGame;
            var player = LocalPlayer;
            if (gunGame == null || player == null)
            {
                return;
            }

            var id = IdOf(player);
            if (id < 0)
            {
                return;
            }

            var next = gunGame.GetNextWeapon(id);
            lines.Add(GunGameText.Tier(gunGame.GetTier(id), gunGame.Ladder.TierCount, gunGame.GetWeapon(id).DisplayName));
            lines.Add(GunGameText.Next(next.HasValue ? next.Value.DisplayName : null));
        }

        private void QueueEquip(SideMember member)
        {
            if (!_pendingEquips.Contains(member))
            {
                _pendingEquips.Add(member);
            }
        }

        private GameRules ResolveWeaponRules()
        {
            var player = LocalPlayer;
            var controller = player != null ? player.GetComponentInChildren<WeaponController>(true) : null;
            return controller != null ? controller.Rules : GameRules.Default;
        }

        /// <summary>member に今の段の武器を持たせる。もう持っていれば何もしない。</summary>
        private void Equip(GunGame gunGame, SideMember member)
        {
            var id = IdOf(member);
            if (id < 0)
            {
                return;
            }

            var tier = gunGame.GetWeaponTier(id);
            var definition = _tierWeapons[tier];
            bool changed;
            try
            {
                changed = EquipController(member, definition) ?? EquipAgent(member, definition) ?? WarnUnarmed(member);
            }
            catch (Exception e) when (e is WeaponDefinitionException || e is ArgumentException)
            {
                // 並びは開始時に検査済みなので、ここに来るのはアセットが Play 中に書き換えられたときだけ。
                Debug.LogError($"{LogTag} '{member.name}' にガンゲームの {tier + 1} 段目の武器を持たせられません: {e.Message}", member);
                return;
            }

            if (changed)
            {
                Debug.Log(GunGameText.Equip(id, tier, gunGame.Ladder.TierCount, gunGame.GetWeapon(id).DisplayName), member);
            }
        }

        /// <summary>プレイヤーの武器を替える。WeaponController が無ければ null、替えたら true、もう持っていれば false。</summary>
        private static bool? EquipController(SideMember member, WeaponDefinition definition)
        {
            var controller = member.GetComponentInChildren<WeaponController>(true);
            if (controller == null)
            {
                return null;
            }

            if (!controller.IsInitialized)
            {
                // Awake で作る前なら、作るときにこの武器だけを持たせる。
                if (controller.Definitions.Count == 1 && controller.Definitions[0] == definition)
                {
                    return false;
                }

                controller.Definitions = new[] { definition };
                return true;
            }

            if (controller.Definitions.Count == 1 && controller.Definition == definition)
            {
                return false;
            }

            controller.Initialize(controller.Aim, controller.AttackAction, controller.ReloadAction, null, null, new[] { definition });
            return true;
        }

        /// <summary>bot の武器を替える。EnemyAgent が無ければ null、替えたら true、もう持っていれば false。</summary>
        private bool? EquipAgent(SideMember member, WeaponDefinition definition)
        {
            var agent = member.GetComponent<EnemyAgent>();
            if (agent == null)
            {
                return null;
            }

            if (agent.WeaponDefinition == definition && agent.WeaponRules == _weaponRules)
            {
                return false;
            }

            agent.EquipWeapon(definition, _weaponRules);
            return true;
        }

        private bool WarnUnarmed(SideMember member)
        {
            if (_warnedUnarmed.Add(member))
            {
                Debug.LogWarning($"{LogTag} '{member.name}' は WeaponController も EnemyAgent も持たないため、ガンゲームの武器を持たせられません。", member);
            }

            return false;
        }
    }
}
