using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    // 人ごとに持たせた能力の種類だけを置かせること・ロードアウトの要求で選んだ能力を次の出現から持たせること・
    // 抜けたときとマップやルールを替えたときに持たせた能力を片付けることを確かめる
    public sealed partial class ServerDeployablesTests
    {
        private const string _noAbility = AbilitySelection.None;

        private const string _rejectedAbilityReason = "ability slot 0 names an ability the rules do not offer";

        [Test]
        public void WithoutListedAbilitiesTheFirstTwoTypesAreHeldAndTheThirdIsRefused()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(
                _log.ToString(),
                Does.Contain("[Deploy] net abilities=deploy-turret,deploy-shield,deploy-decoy default=deploy-turret,deploy-shield from=catalog"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=- net equipped abilities=deploy-turret,deploy-shield"));

            Assert.That(_deployables.Place(1, _decoyId, _alphaFront, 0f), Is.EqualTo(ServerDeployRefusal.NotEquipped));

            Assert.That(Rows(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-decoy net refused reason=NotEquipped"));
            Assert.That(_deployables.Place(1, _shieldId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
        }

        // Issue の確かめ方と同じ: loadout.abilities に Beacon だけを書いたルールで別の種類を置こうとすると NotEquipped で断る
        [Test]
        public void RulesListingOnlyTheBeaconRefuseEveryOtherType()
        {
            Start(EveryRoleCatalog(), loadoutAbilities: Ids(_beaconId));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_log.ToString(), Does.Contain("[Deploy] net abilities=deploy-beacon default=deploy-beacon,- from=rules"));

            Deploy(1, _shieldId, _alphaFront, 90f);

            Assert.That(Rows(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield net refused reason=NotEquipped"));
            Assert.That(_deployables.Place(1, _turretId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.NotEquipped));
            Assert.That(_deployables.Place(1, _decoyId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.NotEquipped));
            Assert.That(_deployables.Place(1, _beaconId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
            Assert.That(Rows(1)[0].Kind, Is.EqualTo(DeployableKind.Beacon));
        }

        [Test]
        public void WithAbilitiesOffTheRuleIsTheReasonEvenForATypeThatIsNotHeld()
        {
            Start(Catalog(), abilities: Abilities.Off, loadoutAbilities: Ids(_shieldId));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, _turretId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.RuleOff));
            Assert.That(_log.ToString(), Does.Not.Contain("reason=NotEquipped"));
        }

        [Test]
        public void AnUnknownTypeIsRefusedAsUnknownBeforeItIsCheckedAgainstTheAbilities()
        {
            Start(Catalog(), loadoutAbilities: Ids(_shieldId));
            Join(1);
            Join(2);
            Step(0.2f);

            Assert.That(_deployables.Place(1, "deploy-missing", _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.UnknownType));
        }

        [Test]
        public void AListedTypeThatIsNotInTheTuningIsLoggedAndNotOffered()
        {
            Start(Catalog(), loadoutAbilities: Ids("deploy-missing", _shieldId));

            Assert.That(
                _log.ToString(),
                Does.Contain("[Deploy] net kind=deploy-missing is in loadout.abilities of rules TeamDeathmatch but not in the deployable tuning; it is not offered"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] net abilities=deploy-shield default=deploy-shield,- from=rules"));
            Assert.That(_rules.Abilities.Candidates, Is.EqualTo(Ids(_shieldId)));
        }

        [Test]
        public void AnUnusableTypeIsNotOffered()
        {
            var drone = new DeployType(_droneId, DeployRole.Drone, DeployType.DefaultDeploy(DeployRole.Drone), drone: DroneSpec.Default);
            var shield = new DeployType(_shieldId, DeployRole.Shield, DeployType.DefaultDeploy(DeployRole.Shield));

            Start(new DeployCatalog(new[] { drone, shield }));

            Assert.That(_log.ToString(), Does.Contain("[Deploy] net abilities=deploy-shield default=deploy-shield,- from=catalog"));
        }

        [Test]
        public void AChosenAbilityIsAnsweredOnceAndHeldFromTheNextSpawn()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            ChooseLoadout(1, _decoyId, _noAbility);

            var answers = Answers(1);
            Assert.That(answers, Has.Count.EqualTo(1), "the weapons and the abilities share one answer");
            Assert.That(answers[0].Accepted, Is.True, _log.ToString());
            Assert.That(answers[0].Reason, Is.Empty);
            Assert.That(
                _log.ToString(),
                Does.Contain("[Net] loadout accepted client=1 weapons=" + TestRules.RifleId + "," + TestRules.HandgunId + " applies=next-spawn"));
            Assert.That(_log.ToString(), Does.Contain("[Net] loadout abilities accepted client=1 abilities=deploy-decoy,- applies=next-spawn"));
            Assert.That(_rules.Abilities.AbilitiesFor(1), Is.EqualTo(Ids(_decoyId, _noAbility)));

            // 生きている間は前の出現で持った能力のまま
            Assert.That(_deployables.Place(1, _decoyId, _alphaFront, 0f), Is.EqualTo(ServerDeployRefusal.NotEquipped));
            Assert.That(_deployables.Place(1, _shieldId, new Vector3(-3f, 0.5f, 2.5f), 90f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_rules.Session.IsAlive(1), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=- net equipped abilities=deploy-decoy,-"));
            Assert.That(_deployables.Place(1, _turretId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.NotEquipped));
            Assert.That(_deployables.Place(1, _decoyId, _alphaFront, 0f), Is.EqualTo(ServerDeployRefusal.None), _log.ToString());
        }

        // Issue の確かめ方と同じ: Ability1 にビーコン・Ability2 に「なし」を選ぶと、次の出現からビーコンだけを置け、ロードアウトの盾は断られる。
        // クライアントの要求 (NetV2LoadoutChooser が送るものと同じ並び) と置く要求を、知らせとしてホストに渡す
        [Test]
        public void ChoosingTheBeaconForAbility1PlacesTheBeaconAndRefusesTheLoadoutShield()
        {
            Start(EveryRoleCatalog(), loadoutAbilities: Ids(_shieldId, _beaconId));
            Join(1);
            Join(2);
            Step(0.2f);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=- net equipped abilities=deploy-shield,deploy-beacon"));

            ChooseLoadout(1, _beaconId, _noAbility);

            var answers = Answers(1);
            Assert.That(answers, Has.Count.EqualTo(1));
            Assert.That(answers[0].Accepted, Is.True, _log.ToString());
            Assert.That(_log.ToString(), Does.Contain("[Net] loadout abilities accepted client=1 abilities=deploy-beacon,- applies=next-spawn"));

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);
            Assert.That(_rules.Session.IsAlive(1), Is.True);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=- net equipped abilities=deploy-beacon,-"));

            Deploy(1, _shieldId, _alphaFront, 90f);

            Assert.That(Rows(1), Is.Empty, "the shield of the loadout is no longer held");
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield net refused reason=NotEquipped"));

            Deploy(1, _beaconId, _alphaFront, 90f);

            var rows = Rows(1);
            Assert.That(rows, Has.Count.EqualTo(1), _log.ToString());
            Assert.That(rows[0].Kind, Is.EqualTo(DeployableKind.Beacon));
            Assert.That(rows[0].TypeId, Is.EqualTo(_beaconId));
            Assert.That(rows[0].OwnerId, Is.EqualTo(1));
        }

        // 「なし」だけを選ぶと、どの種類も置けない
        [Test]
        public void ChoosingNoneForBothSlotsRefusesEveryType()
        {
            Start(EveryRoleCatalog(), loadoutAbilities: Ids(_shieldId, _beaconId));
            Join(1);
            Join(2);
            Step(0.2f);
            ChooseLoadout(1, _noAbility, _noAbility);
            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Deploy(1, _beaconId, _alphaFront, 90f);
            // 断りのログは 1 秒に 1 回までなので、2 つ目の断りも出るよう間を空ける
            Step(1.5f);
            Deploy(1, _shieldId, _alphaFront, 90f);

            Assert.That(Rows(1), Is.Empty);
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-beacon net refused reason=NotEquipped"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=deploy-shield net refused reason=NotEquipped"));
        }

        [Test]
        public void AnAbilityTheRulesDoNotOfferRefusesTheWholeRequest()
        {
            Start(Catalog(), loadoutAbilities: Ids(_shieldId));
            Join(1);
            Join(2);
            Step(0.2f);

            var request = new LoadoutRequestMessage();
            request.WeaponIds.Add(TestRules.HandgunId);
            request.WeaponIds.Add(TestRules.RifleId);
            request.AbilityIds.Add(_decoyId);
            request.AbilityIds.Add(_noAbility);
            _host.OnMessage(new ConnectionId(1), request);

            var answers = Answers(1);
            Assert.That(answers, Has.Count.EqualTo(1));
            Assert.That(answers[0].Accepted, Is.False);
            Assert.That(answers[0].Reason, Is.EqualTo(_rejectedAbilityReason));
            Assert.That(_log.ToString(), Does.Contain("[Net] loadout abilities rejected client=1 reason=" + _rejectedAbilityReason));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] loadout accepted client=1"), "the weapons of a refused request are not chosen either");
            Assert.That(_rules.Loadout.HasChoice(1), Is.False);
            Assert.That(_rules.Abilities.HasChoice(1), Is.False);
            Assert.That(_rules.Abilities.AbilitiesFor(1), Is.EqualTo(Ids(_shieldId, _noAbility)));
        }

        [Test]
        public void ARefusedWeaponChoiceDoesNotChooseTheAbilities()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);

            var request = new LoadoutRequestMessage();
            request.WeaponIds.Add("not-a-weapon");
            request.AbilityIds.Add(_decoyId);
            request.AbilityIds.Add(_noAbility);
            _host.OnMessage(new ConnectionId(1), request);

            var answers = Answers(1);
            Assert.That(answers, Has.Count.EqualTo(1));
            Assert.That(answers[0].Accepted, Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Net] loadout rejected client=1"));
            Assert.That(_log.ToString(), Does.Not.Contain("[Net] loadout abilities "));
            Assert.That(_rules.Abilities.HasChoice(1), Is.False);
        }

        [Test]
        public void ARequestWithoutAbilitiesKeepsTheChosenOnes()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            ChooseLoadout(1, _decoyId, _noAbility);

            // 能力の枠を持たない前の版のクライアントからの要求と同じ
            ChooseLoadout(1);

            var answers = Answers(1);
            Assert.That(answers, Has.Count.EqualTo(2));
            Assert.That(answers[1].Accepted, Is.True);
            Assert.That(_rules.Abilities.AbilitiesFor(1), Is.EqualTo(Ids(_decoyId, _noAbility)));
            Assert.That(_log.ToString().Split("[Net] loadout abilities accepted").Length - 1, Is.EqualTo(1));
        }

        [Test]
        public void EmptySlotsHoldNothingToPlace()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            ChooseLoadout(1, _noAbility, _noAbility);

            _combat.DealDamage(2, 1, NetMatchSettings.DefaultMaxHealth, TestRules.RifleId, HitZone.Body, null);
            Step(_respawnWait);

            Assert.That(_log.ToString(), Does.Contain("[Net] loadout abilities accepted client=1 abilities=-,- applies=next-spawn"));
            Assert.That(_log.ToString(), Does.Contain("[Deploy] owner=1 kind=- net equipped abilities=-,-"));
            Assert.That(_deployables.Place(1, _shieldId, _alphaFront, 90f), Is.EqualTo(ServerDeployRefusal.NotEquipped));
        }

        [Test]
        public void LeavingForgetsTheChosenAbilities()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            ChooseLoadout(1, _decoyId, _noAbility);
            Assert.That(_rules.Abilities.HasChoice(1), Is.True);

            _match.Leave(new ConnectionId(1));
            _host.OnDisconnected(new ConnectionId(1));

            Assert.That(_rules.Abilities.HasChoice(1), Is.False);
            Assert.That(_rules.Abilities.AbilitiesFor(1), Is.EqualTo(Ids(_turretId, _shieldId)));
        }

        [Test]
        public void ChangingTheMapThrowsAwayWhatWasHeldAndKeepsTheChoice()
        {
            Start(Catalog(), otherMap: true);
            Join(1);
            Join(2);
            Step(0.2f);
            ChooseLoadout(1, _decoyId, _noAbility);
            var before = _log.ToString().Length;

            Assert.That(_match.RequestNextMap(_otherMapName), Is.True);
            _rules.Session.Finish(Team.Alpha);
            Step(NetMatchSettings.DefaultRestartDelaySeconds + 0.5f);

            var written = _log.ToString().Substring(before);
            Assert.That(_match.Map.Name, Is.EqualTo(_otherMapName), written);
            Assert.That(written, Does.Contain("[Deploy] owner=1 kind=- net equipped abilities=deploy-decoy,-"));
            Assert.That(
                written,
                Does.Contain("[Deploy] owner=2 kind=- net equipped abilities=deploy-turret,deploy-shield"),
                "the default held on the old map is given again");
            Assert.That(_rules.Abilities.HasChoice(1), Is.True);
            Assert.That(_deployables.Place(1, _decoyId, _alphaFront, 0f), Is.EqualTo(ServerDeployRefusal.None), written);
        }

        [Test]
        public void ChangingTheRulesReadsTheCandidatesAgainAndThrowsAwayWhatWasHeld()
        {
            Start(Catalog());
            Join(1);
            Join(2);
            Step(0.2f);
            ChooseLoadout(1, _decoyId, _noAbility);
            var before = _log.ToString().Length;

            Assert.That(_match.RequestNextRules(Rules(Abilities.On), out var error), Is.True, error);
            _rules.Session.Finish(Team.Alpha);
            Step(NetMatchSettings.DefaultRestartDelaySeconds + 0.5f);

            var written = _log.ToString().Substring(before);
            Assert.That(
                written,
                Does.Contain("[Deploy] net abilities=deploy-turret,deploy-shield,deploy-decoy default=deploy-turret,deploy-shield from=catalog"));
            Assert.That(written, Does.Contain("[Deploy] owner=1 kind=- net equipped abilities=deploy-decoy,-"));
            Assert.That(written, Does.Contain("[Deploy] owner=2 kind=- net equipped abilities=deploy-turret,deploy-shield"));
            Assert.That(_rules.Abilities.HasChoice(1), Is.True, "the same file still offers the decoy");
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Ids(params string[] ids) => ids;

        private static DeployCatalog EveryRoleCatalog()
        {
            return new DeployCatalog(new[]
            {
                new DeployType(_turretId, DeployRole.Turret, DeployType.DefaultDeploy(DeployRole.Turret), TurretSpec.Default, TestRules.RifleId),
                new DeployType(_shieldId, DeployRole.Shield, DeployType.DefaultDeploy(DeployRole.Shield)),
                new DeployType(_decoyId, DeployRole.Decoy, DeployType.DefaultDeploy(DeployRole.Decoy), decoy: DecoySpec.Default),
                new DeployType(_beaconId, DeployRole.Beacon, DeployType.DefaultDeploy(DeployRole.Beacon), beacon: BeaconSpec.Default),
            });
        }

        // 武器は既定の並び (ライフル・拳銃) のまま送る
        private void ChooseLoadout(int playerId, params string[] abilityIds)
        {
            var request = new LoadoutRequestMessage();
            request.WeaponIds.Add(TestRules.RifleId);
            request.WeaponIds.Add(TestRules.HandgunId);
            foreach (var id in abilityIds)
            {
                request.AbilityIds.Add(id);
            }

            _host.OnMessage(new ConnectionId((ulong)playerId), request);
        }

        private List<LoadoutAnswerMessage> Answers(int playerId)
        {
            return _transport.Read<LoadoutAnswerMessage>(playerId, m => new LoadoutAnswerMessage { Accepted = m.Accepted, Reason = m.Reason });
        }
    }
}
