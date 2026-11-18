using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 最初の感染者を出現点へ戻すのを ModeSession の Tick の後に回すのは、感染の知らせが Tick の途中 (目標を進める間) に来るため。
    // 倒されて感染した人の武器は、戻ったとき (OnSpawned) に替える。倒れた瞬間はダメージの処理の途中なので持ち替えない。
    public sealed class InfectedRunner : ModeRunnerBase
    {
        public const int DefaultInitialInfected = InfectedSpec.DefaultInitialInfected;
        public const float DefaultStartDelaySeconds = InfectedSpec.DefaultStartDelaySeconds;
        public const float DefaultRoundSeconds = InfectedSpec.DefaultRoundSeconds;
        public const int DefaultInfectedMaxHealth = InfectedSpec.DefaultInfectedMaxHealth;
        public const float DefaultInfectedSpeedMultiplier = InfectedSpec.DefaultInfectedSpeedMultiplier;
        public const float DefaultInfectedRespawnSeconds = InfectedSpec.DefaultInfectedRespawnSeconds;
        public const int DefaultScorePerInfection = InfectedSpec.DefaultScorePerInfection;
        public const int DefaultScorePerSurvivedRound = InfectedSpec.DefaultScorePerSurvivedRound;

        [SerializeField, Tooltip("このモードの調整値と感染者の武器のアセット。空なら調整値の JSON (GameContent の InfectedTuning か Resources/Tuning/infected) を読む")]
        private InfectedDefinition _definition;

        [SerializeField, Tooltip("調整値の JSON に書いた感染者の武器の Id を引くカタログ。空なら入口 (GameContent) のものを使う")]
        private WeaponCatalogAsset _weaponCatalog;

        private readonly List<SideMember> _pendingFirst = new List<SideMember>();
        private readonly HashSet<SideMember> _warnedUnarmed = new HashSet<SideMember>();
        private Infected _infected;
        private WeaponDefinition _infectedWeapon;
        private string _infectedWeaponName = string.Empty;
        private GameRules _weaponRules = GameRules.Default;
        private Func<int, int> _outbreakPicker;
        private bool _loggedOutbreak;

        public Infected Infected => _infected;

        public InfectedDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public WeaponCatalogAsset WeaponCatalog
        {
            get => _weaponCatalog;
            set => _weaponCatalog = value;
        }

        // 0 から count 未満の添字を返す。空なら Infected が乱数で選ぶ。テストで最初の感染者を決まったものにするために入れられるようにしておく。
        public Func<int, int> OutbreakPicker
        {
            get => _outbreakPicker;
            set => _outbreakPicker = value;
        }

        public WeaponDefinition InfectedWeapon => _infectedWeapon;

        public InfectedSpec CreateSpec()
        {
            return _definition != null ? _definition.ToSpec() : KitTuning.Infected;
        }

        public WeaponDefinition ResolveInfectedWeapon(InfectedSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_definition != null && _definition.InfectedWeapon != null)
            {
                var assigned = _definition.InfectedWeapon;
                try
                {
                    assigned.ToSpec();
                }
                catch (WeaponDefinitionException e)
                {
                    throw new InvalidOperationException($"infected definition '{_definition.name}' has an invalid infected weapon: {e.Message}", e);
                }

                return assigned;
            }

            if (!spec.HasInfectedWeapon)
            {
                return null;
            }

            var id = spec.InfectedWeaponId;
            var catalog = GameContent.Pick(_weaponCatalog, c => c.WeaponCatalog);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(InfectedRunner)} '{name}' has no weapon catalog to find the infected weapon '{id}'; wire WeaponCatalog or set WeaponCatalog in the GameContent");
            }

            WeaponDefinition weapon;
            try
            {
                if (catalog.TryFind(id, out weapon))
                {
                    return weapon;
                }
            }
            catch (WeaponDefinitionException e)
            {
                throw new InvalidOperationException($"weapon catalog '{catalog.name}' is invalid: {e.Message}", e);
            }

            throw new InvalidOperationException($"weapon catalog '{catalog.name}' has no weapon with the infected weapon id '{id}'");
        }

        public void Begin(
            Infected infected,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (infected == null)
            {
                throw new ArgumentNullException(nameof(infected));
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(InfectedRunner)} '{name}' has already started a mode");
            }

            Attach(infected, ResolveInfectedWeapon(infected.Spec));
            ToSurvivors(members);
            ToSurvivors(localPlayer);
            try
            {
                Begin(infected.Session, members, spawnPoints, localPlayer);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        protected override ModeSession CreateSession()
        {
            var spec = CreateSpec();
            var weapon = ResolveInfectedWeapon(spec);
            var infected = new Infected(spec, _outbreakPicker);
            Attach(infected, weapon);
            ToSurvivors(SideMember.FindAll());
            return infected.Session;
        }

        protected override void OnBegan()
        {
            _weaponRules = ModeLoadout.RulesOf(LocalPlayer);
            if (Members.Count < 2)
            {
                Debug.LogWarning($"{LogTag} {nameof(InfectedRunner)} '{name}' は参加者が {Members.Count} 人なので、感染者を選べません。時間切れで生存者の勝ちになります。", this);
            }
        }

        protected override void AdvanceSession(float deltaSeconds)
        {
            base.AdvanceSession(deltaSeconds);
            if (_infected == null)
            {
                return;
            }

            if (_infected.HasBrokenOut && !_loggedOutbreak)
            {
                _loggedOutbreak = true;
                Debug.Log(InfectedLog.Outbreak(_infected.FirstInfectedIds), this);
            }

            RespawnFirstInfected();
        }

        protected override void OnKill(SideMember killer, SideMember victim)
        {
            if (_infected == null || victim == null)
            {
                return;
            }

            var victimId = IdOf(victim);
            if (victimId < 0)
            {
                return;
            }

            var killerId = IdOf(killer);
            _infected.Infect(victimId, killerId >= 0 ? killerId : (int?)null);
        }

        protected override void OnSpawned(SideMember member)
        {
            if (_infected == null)
            {
                return;
            }

            var id = IdOf(member);
            if (id >= 0 && _infected.IsInfected(id))
            {
                EquipInfected(member, id);
            }
        }

        protected override void OnEnded(Side? winner)
        {
            _pendingFirst.Clear();
        }

        protected override object HudDetail
        {
            get
            {
                if (_infected == null)
                {
                    return null;
                }

                var id = IdOf(LocalPlayer);
                return InfectedStatus.Of(_infected, id >= 0 ? id : (int?)null);
            }
        }

        protected override void OnStopping()
        {
            Detach();
        }

        private void Attach(Infected infected, WeaponDefinition weapon)
        {
            Detach();
            _infected = infected;
            _infectedWeapon = weapon;
            _infectedWeaponName = weapon != null ? WeaponName(weapon) : string.Empty;
            _infected.Turned += OnTurned;
        }

        private void Detach()
        {
            if (_infected != null)
            {
                _infected.Turned -= OnTurned;
            }

            _infected = null;
            _infectedWeapon = null;
            _infectedWeaponName = string.Empty;
            _pendingFirst.Clear();
            _warnedUnarmed.Clear();
            _loggedOutbreak = false;
        }

        // シーンに置いた陣営を使わず全員を生存者から始めるのは、感染者は始まりの秒数の後にモードが選ぶため。
        private static void ToSurvivors(IReadOnlyList<SideMember> members)
        {
            foreach (var member in members)
            {
                ToSurvivors(member);
            }
        }

        private static void ToSurvivors(SideMember member)
        {
            if (member != null)
            {
                member.Side = Infected.SurvivorSide;
            }
        }

        private void OnTurned(InfectedTurn turn)
        {
            var member = MemberOf(turn.PlayerId);
            if (member == null)
            {
                return;
            }

            Debug.Log(InfectedLog.Turn(turn), member);
            member.Side = Infected.InfectedSide;
            ApplyInfectedBody(member);
            if (turn.IsFirst && !_pendingFirst.Contains(member))
            {
                _pendingFirst.Add(member);
            }
        }

        // 倒れている最初の感染者は、ModeSession の待ちが明けたときに戻る (そこで武器も替わる)。
        private void RespawnFirstInfected()
        {
            if (_pendingFirst.Count == 0)
            {
                return;
            }

            var session = Session;
            foreach (var member in _pendingFirst)
            {
                if (session == null || session.State != GameModeState.Playing)
                {
                    break;
                }

                var id = IdOf(member);
                if (member != null && id >= 0 && !session.IsDown(id))
                {
                    Respawn(member);
                }
            }

            _pendingFirst.Clear();
        }

        private void ApplyInfectedBody(SideMember member)
        {
            var spec = _infected.Spec;
            if (member.Damageable != null)
            {
                member.Damageable.MaxHealth = spec.InfectedMaxHealth;
            }

            var agent = member.GetComponent<EnemyAgent>();
            if (agent != null)
            {
                agent.MoveSpeedMultiplier = spec.InfectedSpeedMultiplier;
            }
        }

        private void EquipInfected(SideMember member, int id)
        {
            if (_infectedWeapon == null)
            {
                return;
            }

            bool changed;
            try
            {
                changed = ModeLoadout.EquipController(member, _infectedWeapon)
                    ?? ModeLoadout.EquipAgent(member, _infectedWeapon, _weaponRules)
                    ?? WarnUnarmed(member);
            }
            catch (Exception e) when (e is WeaponDefinitionException || e is ArgumentException)
            {
                Debug.LogError($"{LogTag} '{member.name}' に感染者の武器を持たせられません: {e.Message}", member);
                return;
            }

            if (changed)
            {
                Debug.Log(InfectedLog.Equip(id, _infectedWeaponName), member);
            }
        }

        private bool WarnUnarmed(SideMember member)
        {
            if (_warnedUnarmed.Add(member))
            {
                Debug.LogWarning($"{LogTag} '{member.name}' は WeaponController も EnemyAgent も持たないため、感染者の武器を持たせられません。", member);
            }

            return false;
        }

        private static string WeaponName(WeaponDefinition weapon)
        {
            try
            {
                var spec = weapon.ToSpec();
                return string.IsNullOrEmpty(spec.NameKey) ? spec.Id : spec.NameKey;
            }
            catch (WeaponDefinitionException)
            {
                return string.Empty;
            }
        }
    }
}
