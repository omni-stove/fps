using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 段が変わった人の武器を次の Tick の頭で替えるのは、撃っている最中の WeaponController・EnemyAgent の武器を差し替えないため。
    public sealed class GunGameRunner : ModeRunnerBase
    {
        public const float DefaultTimeLimitSeconds = GunGameSpec.DefaultTimeLimitSeconds;
        public const float DefaultRespawnSeconds = GunGameSpec.DefaultRespawnSeconds;

        [SerializeField, Tooltip("武器の並びと制限時間・リスポーンの秒数。空なら入口 (GameContent) のものを使う")]
        private GunGameLadderDefinition _ladder;

        private readonly List<SideMember> _pendingEquips = new List<SideMember>();
        private readonly HashSet<SideMember> _warnedUnarmed = new HashSet<SideMember>();
        private readonly List<WeaponDefinition> _tierWeapons = new List<WeaponDefinition>();
        private WeaponDefinition _meleeWeapon;
        private GunGame _gunGame;
        private ModeSession _gunGameSession;
        private GameRules _weaponRules = GameRules.Default;

        public GunGameLadderDefinition Ladder
        {
            get => _ladder;
            set => _ladder = value;
        }

        public GunGameLadderDefinition EffectiveLadder => GameContent.Pick(_ladder, c => c.GunGameLadder);

        public GunGame GunGame => _gunGame != null && Session != null && Session == _gunGameSession ? _gunGame : null;

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

        public ModeSession CreateGunGameSession()
        {
            var definition = EffectiveLadder;
            if (definition == null)
            {
                throw new InvalidOperationException(
                    $"GunGameRunner '{name}' has no GunGameLadderDefinition; wire Ladder or set GunGameLadder in the GameContent");
            }

            GunGameLadder ladder;
            try
            {
                ladder = definition.ToLadder();
            }
            catch (WeaponDefinitionException e)
            {
                throw new InvalidOperationException($"gun game ladder '{definition.name}' is invalid: {e.Message}", e);
            }

            var gunGame = new GunGame(ladder);
            var session = definition.CreateSession(gunGame);

            _gunGame = gunGame;
            _gunGameSession = session;
            _tierWeapons.Clear();
            _tierWeapons.AddRange(definition.Tiers);
            _meleeWeapon = definition.MeleeWeapons.Count > 0 ? definition.MeleeWeapons[0] : null;
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

            _weaponRules = ModeLoadout.RulesOf(LocalPlayer);
            _pendingEquips.Clear();
            foreach (var member in Members)
            {
                Equip(gunGame, member);
                EquipMelee(member);
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
                Debug.Log(GunGameLog.Demote(kill.VictimId, kill.VictimTier, count, gunGame.GetWeapon(kill.VictimId).LogName), victim);
                QueueEquip(victim);
            }

            if (kill.Finished)
            {
                Debug.Log(GunGameLog.Finish(kill.KillerId, gunGame.GetWeapon(kill.KillerId).LogName), killer);
            }
            else if (kill.Promoted)
            {
                Debug.Log(GunGameLog.Promote(kill.KillerId, kill.KillerTier, count, gunGame.GetWeapon(kill.KillerId).LogName), killer);
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

        protected override object HudDetail
        {
            get
            {
                var gunGame = GunGame;
                if (gunGame == null)
                {
                    return null;
                }

                var id = IdOf(LocalPlayer);
                return new GunGameStatus(gunGame, id >= 0 ? id : (int?)null);
            }
        }

        private void QueueEquip(SideMember member)
        {
            if (!_pendingEquips.Contains(member))
            {
                _pendingEquips.Add(member);
            }
        }

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
                changed = ModeLoadout.EquipController(member, definition)
                    ?? ModeLoadout.EquipAgent(member, definition, _weaponRules)
                    ?? WarnUnarmed(member);
            }
            catch (Exception e) when (e is WeaponDefinitionException || e is ArgumentException)
            {
                // 並びは開始時に検査済みなので、ここに来るのはアセットが Play 中に書き換えられたときだけ。
                Debug.LogError($"{LogTag} '{member.name}' にガンゲームの {tier + 1} 段目の武器を持たせられません: {e.Message}", member);
                return;
            }

            if (changed)
            {
                Debug.Log(GunGameLog.Equip(id, tier, gunGame.Ladder.TierCount, gunGame.GetWeapon(id).LogName), member);
            }
        }

        // 近接に並びの最初の近接の武器を持たせるのは、持っている段の武器の Id のままだと、近接で倒しても段の武器で倒したことになり、相手を戻せないため。
        private void EquipMelee(SideMember member)
        {
            if (_meleeWeapon == null)
            {
                return;
            }

            var melee = member.GetComponentInChildren<MeleeController>(true);
            if (melee != null)
            {
                melee.MeleeWeapon = _meleeWeapon;
            }
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
