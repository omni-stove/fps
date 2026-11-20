using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Editor;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    // 画面で決めた能力を要求に載せ、受け入れられた能力で NetV2DeployInputPart の枠を差し替える。
    // つなぎ込み (NetV2ClientParts / NetV2Client) はまだ無いので、テストで AbilitiesAccepted を SetSlots につなぐ。
    public sealed class NetV2LoadoutAbilitiesTests
    {
        private const string StoreKey = "Shooter.Tests.NetV2LoadoutAbilities";
        private const string RifleId = "00000000-0000-4000-8000-0000000000a1";
        private const string HandgunId = "00000000-0000-4000-8000-0000000000a2";
        private const string ShieldId = "deploy-shield";
        private const string BeaconId = "deploy-beacon";
        private const int LocalPlayerId = 1;
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<SentRequest> _requests = new List<SentRequest>();
        private readonly List<string> _deployLogs = new List<string>();
        private LoadoutPreferenceStore _store;
        private GameContent _content;
        private LoadoutDefinition _loadout;
        private AbilityCatalogAsset _abilities;
        private ShieldDeployDefinition _shield;
        private BeaconDeployDefinition _beacon;
        private FakeLoadoutSelect _view;
        private NetV2LoadoutChooser _chooser;
        private NetV2DeployablesPart _deployables;
        private NetV2DeployInputPart _input;
        private NetV2LocalPlayer _local;
        private int _pressedSlot;
        private bool _connected;

        [SetUp]
        public void SetUp()
        {
            _requests.Clear();
            _deployLogs.Clear();
            _connected = true;
            _pressedSlot = -1;
            _store = new LoadoutPreferenceStore(StoreKey);
            _store.Delete();

            var rifle = Weapon("Rifle", new WeaponSpec(
                RifleId, "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None));
            var handgun = Weapon("Handgun", new WeaponSpec(
                HandgunId, "Handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None));

            _loadout = Create<LoadoutDefinition>("TestLoadout");
            WeaponAssets.Configure(
                _loadout,
                new[]
                {
                    new LoadoutSlotDefinition(new[] { WeaponCategory.AssaultRifle }, rifle),
                    new LoadoutSlotDefinition(new[] { WeaponCategory.Handgun }, handgun),
                },
                LoadoutDefinition.DefaultSwitchSeconds);
            var weapons = Create<WeaponCatalogAsset>("TestWeapons");
            WeaponAssets.Configure(weapons, new[] { rifle, handgun });

            _shield = Create<ShieldDeployDefinition>("ShieldAsset");
            _shield.Id = ShieldId;
            _beacon = Create<BeaconDeployDefinition>("BeaconAsset");
            _beacon.Id = BeaconId;

            // つないだときの枠は、ロードアウトの能力 (盾だけ) で決まる
            _loadout.SetAbilities(new AbilityDefinition[] { _shield });
            _abilities = Create<AbilityCatalogAsset>("TestAbilities");
            _abilities.SetAbilities(new AbilityDefinition[] { _shield, _beacon });

            _content = Create<GameContent>("TestContent");
            _content.Loadout = _loadout;
            _content.WeaponCatalog = weapons;
            _content.AbilityCatalog = _abilities;
            GameContent.Use(_content);

            _view = new FakeLoadoutSelect();
            _chooser = new NetV2LoadoutChooser(new NetV2Weapons(), Send, null, () => string.Empty, _store);
            _chooser.Bind(_view);

            _local = NetV2LocalPlayer.Create(_stance, MotorSpec.Default, () => true, Vector3.zero, 0f);
            _created.Add(_local.gameObject);
            _deployables = new NetV2DeployablesPart(
                _ => true, () => LocalPlayerId, new DeployAbilityDefinition[] { _shield, _beacon }, _deployLogs.Add);
            _input = new NetV2DeployInputPart(
                _deployables, NetV2DeployInputPart.SlotsOf(_loadout.Abilities), () => true, () => _local, slot => slot == _pressedSlot);
            _input.Apply(AbilitiesOn());
            _chooser.AbilitiesAccepted += ids => _input.SetSlots(NetV2DeployInputPart.SlotsOf(ids, _abilities));
        }

        [TearDown]
        public void TearDown()
        {
            _chooser.Dispose();
            _input.Dispose();
            _deployables.Dispose();
            GameContent.Use(null);
            _store.Delete();
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ChoosingOneAbilityPutsItsIdInTheRequest()
        {
            OpenScreen();
            Assert.That(_view.Abilities, Is.Not.Null, "the catalog offers abilities");
            Assert.That(_view.Abilities.AbilityIds, Is.EqualTo(new[] { ShieldId, AbilitySelection.None }), "the screen starts from the loadout's abilities");

            Assert.That(_view.Abilities.Select(1, _view.Abilities.OptionsAt(1).ToList().IndexOf(BeaconId)), Is.True);
            _view.Confirm();

            Assert.That(_requests, Has.Count.EqualTo(1));
            Assert.That(_requests[0].WeaponIds, Is.EqualTo(new[] { RifleId, HandgunId }));
            Assert.That(_requests[0].AbilityIds, Is.EqualTo(new[] { ShieldId, BeaconId }));
            Assert.That(_view.SendingShown, Is.True);
        }

        [Test]
        public void AnAcceptedAnswerReplacesTheSlotsAndSavesTheWeaponsWithTheAbilities()
        {
            var raised = new List<IReadOnlyList<string>>();
            _chooser.AbilitiesAccepted += raised.Add;
            ConfirmBeaconInTheFirstSlot();

            Assert.That(_input.Slots[0], Is.SameAs(_shield), "the slots do not change before the server answers");
            Assert.That(_store.Load(), Is.Null, "nothing is saved before the server answers");

            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(raised, Has.Count.EqualTo(1));
            Assert.That(raised[0], Is.EqualTo(new[] { BeaconId, AbilitySelection.None }));
            Assert.That(_chooser.AcceptedAbilityIds, Is.EqualTo(new[] { BeaconId, AbilitySelection.None }));
            Assert.That(_input.Slots[0], Is.SameAs(_beacon));
            Assert.That(_input.Slots[1], Is.Null);
            Assert.That(_view.Answers, Is.EqualTo(new[] { "accepted" }));

            var saved = _store.Load();
            Assert.That(saved.WeaponIds, Is.EqualTo(new[] { RifleId, HandgunId }));
            Assert.That(saved.AbilityIds, Is.EqualTo(new[] { BeaconId, AbilitySelection.None }));

            _pressedSlot = 0;
            _input.Tick(0.1f);

            Assert.That(_deployLogs.Any(line => line.Contains("kind=" + BeaconId)), Is.True, string.Join("\n", _deployLogs));
            Assert.That(_deployLogs.Any(line => line.Contains("kind=" + ShieldId)), Is.False, "Ability1 no longer places the loadout's shield");
        }

        [Test]
        public void ARejectedAnswerLeavesTheSlotsAndTheSavedChoiceAsTheyWere()
        {
            var raised = 0;
            _chooser.AbilitiesAccepted += _ => raised++;
            ConfirmBeaconInTheFirstSlot();

            _chooser.Answer(new LoadoutAnswerMessage { Accepted = false, Reason = "ability slot 0 names deploy-beacon, which is not offered" });

            Assert.That(raised, Is.Zero);
            Assert.That(_chooser.AcceptedAbilityIds, Is.Null);
            Assert.That(_input.Slots[0], Is.SameAs(_shield));
            Assert.That(_input.Slots[1], Is.Null);
            Assert.That(_store.Load(), Is.Null);
            Assert.That(_view.Answers, Is.EqualTo(new[] { "rejected:ability slot 0 names deploy-beacon, which is not offered" }));

            _pressedSlot = 0;
            _input.Tick(0.1f);

            Assert.That(_deployLogs.Any(line => line.Contains("kind=" + ShieldId)), Is.True, string.Join("\n", _deployLogs));
        }

        [Test]
        public void EachAnswerIsMatchedWithTheAbilitiesOfTheRequestItAnswers()
        {
            var raised = new List<IReadOnlyList<string>>();
            _chooser.AbilitiesAccepted += raised.Add;
            ConfirmBeaconInTheFirstSlot();
            _view.Abilities.Select(0, AbilitySelection.NoneIndex);
            _view.Confirm();

            _chooser.Answer(new LoadoutAnswerMessage { Accepted = false, Reason = "too soon" });
            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(raised, Has.Count.EqualTo(1));
            Assert.That(raised[0], Is.EqualTo(new[] { AbilitySelection.None, AbilitySelection.None }));
            Assert.That(_input.Slots.All(slot => slot == null), Is.True, "choosing none in every slot empties both slots");
        }

        [Test]
        public void TheSavedAbilitiesAreSentWithTheSavedWeapons()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { AbilitySelection.None, BeaconId }));

            _chooser.SendSaved();

            Assert.That(_requests, Has.Count.EqualTo(1));
            Assert.That(_requests[0].WeaponIds, Is.EqualTo(new[] { RifleId, HandgunId }));
            Assert.That(_requests[0].AbilityIds, Is.EqualTo(new[] { AbilitySelection.None, BeaconId }));

            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(_input.Slots[0], Is.Null);
            Assert.That(_input.Slots[1], Is.SameAs(_beacon));
        }

        [Test]
        public void SavedAbilitiesThisMatchDoesNotOfferAreLeftOutButTheWeaponsAreStillSent()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { "deploy-missing", BeaconId }));
            var raised = 0;
            _chooser.AbilitiesAccepted += _ => raised++;

            _chooser.SendSaved();

            Assert.That(_requests, Has.Count.EqualTo(1));
            Assert.That(_requests[0].WeaponIds, Is.EqualTo(new[] { RifleId, HandgunId }));
            Assert.That(_requests[0].AbilityIds, Is.Empty, "the server keeps the loadout's abilities");

            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(raised, Is.Zero);
            Assert.That(_input.Slots[0], Is.SameAs(_shield));
            Assert.That(_store.Load().AbilityIds, Is.Null, "the abilities that were not sent are not kept as accepted");
        }

        [Test]
        public void WithoutAnAbilityCatalogOnlyTheWeaponsAreSentAndTheSlotsStay()
        {
            _content.AbilityCatalog = null;
            var raised = 0;
            _chooser.AbilitiesAccepted += _ => raised++;

            OpenScreen();
            Assert.That(_view.Abilities, Is.Null);
            _view.Confirm();
            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(_requests, Has.Count.EqualTo(1));
            Assert.That(_requests[0].AbilityIds, Is.Empty);
            Assert.That(raised, Is.Zero);
            Assert.That(_chooser.AcceptedAbilityIds, Is.Null);
            Assert.That(_input.Slots[0], Is.SameAs(_shield));
            Assert.That(_store.Load().AbilityIds, Is.Null);
        }

        [Test]
        public void ARequestThatCouldNotBeSentIsNotAnswered()
        {
            OpenScreen();
            _connected = false;
            _view.Confirm();

            Assert.That(_view.Answers, Is.EqualTo(new[] { "rejected:not connected to the match" }));

            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(_chooser.AcceptedAbilityIds, Is.Null);
            Assert.That(_input.Slots[0], Is.SameAs(_shield));
        }

        [Test]
        public void SlotsOfIdsLooksTheDeployAbilitiesUpInTheCatalog()
        {
            var grapple = Create<GrappleAbilityDefinition>("GrappleAsset");
            grapple.Id = "grapple";
            _abilities.SetAbilities(new AbilityDefinition[] { _shield, _beacon, grapple });

            var slots = NetV2DeployInputPart.SlotsOf(new[] { "grapple", BeaconId, ShieldId }, _abilities);

            Assert.That(slots.Count, Is.EqualTo(NetV2DeployInputPart.SlotCount));
            Assert.That(slots[0], Is.Null, "an ability that does not place anything leaves the slot empty");
            Assert.That(slots[1], Is.SameAs(_beacon));

            var unknown = NetV2DeployInputPart.SlotsOf(new[] { "deploy-missing", AbilitySelection.None }, _abilities);
            Assert.That(unknown.All(slot => slot == null), Is.True);
            Assert.That(NetV2DeployInputPart.SlotsOf(new[] { BeaconId }, null).All(slot => slot == null), Is.True);
            Assert.That(NetV2DeployInputPart.SlotsOf((IReadOnlyList<string>)null, _abilities).All(slot => slot == null), Is.True);
        }

        [Test]
        public void SetSlotsKeepsItsOwnCopyAndRefusesNull()
        {
            var slots = new DeployAbilityDefinition[] { _beacon, _shield };

            _input.SetSlots(slots);
            slots[0] = null;

            Assert.That(_input.Slots[0], Is.SameAs(_beacon));
            Assert.That(_input.Slots[1], Is.SameAs(_shield));
            Assert.Throws<ArgumentNullException>(() => _input.SetSlots(null));
        }

        private void ConfirmBeaconInTheFirstSlot()
        {
            OpenScreen();
            Assert.That(_view.Abilities.Select(0, _view.Abilities.OptionsAt(0).ToList().IndexOf(BeaconId)), Is.True);
            _view.Confirm();
            Assert.That(_requests.Last().AbilityIds, Is.EqualTo(new[] { BeaconId, AbilitySelection.None }));
        }

        // 自分が倒れて試合が続いている間に画面が開く
        private void OpenScreen()
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.State = GameModeState.Playing;
            ref var own = ref snapshot.Players.Add();
            own.PlayerId = LocalPlayerId;
            own.Health = 0;
            _chooser.Apply(snapshot, LocalPlayerId);
            Assert.That(_view.IsOpen, Is.True);
        }

        private static SnapshotMessage AbilitiesOn()
        {
            var snapshot = new SnapshotMessage();
            var rules = snapshot.Match.Rules;
            rules.Abilities = Shooter.Core.Abilities.On;
            snapshot.Match.Rules = rules;
            return snapshot;
        }

        // 要求の入れ物は送るたびに使い回されるので、送った時点の中身を写す
        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            if (message is LoadoutRequestMessage request)
            {
                _requests.Add(new SentRequest(Ids(request.WeaponIds), Ids(request.AbilityIds)));
            }

            return true;
        }

        private static string[] Ids(ContentIdList list)
        {
            var ids = new string[list.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = list[i];
            }

            return ids;
        }

        private WeaponDefinition Weapon(string assetName, WeaponSpec spec)
        {
            var definition = Create<WeaponDefinition>(assetName);
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private T Create<T>(string assetName)
            where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = assetName;
            _created.Add(asset);
            return asset;
        }

        private sealed class SentRequest
        {
            public SentRequest(string[] weaponIds, string[] abilityIds)
            {
                WeaponIds = weaponIds;
                AbilityIds = abilityIds;
            }

            public string[] WeaponIds { get; }

            public string[] AbilityIds { get; }
        }

        private sealed class FakeLoadoutSelect : ILoadoutSelect
        {
            public event Action<LoadoutChoice> Confirmed;

            public bool IsAnyOpen => IsOpen;

            public bool IsOpen { get; private set; }

            public LoadoutSelection Weapons { get; private set; }

            public AbilitySelection Abilities { get; private set; }

            public bool SendingShown { get; private set; }

            public List<string> Answers { get; } = new List<string>();

            public void Open(LoadoutSelection selection, AbilitySelection abilities)
            {
                Weapons = selection;
                Abilities = abilities;
                IsOpen = true;
            }

            public void Close()
            {
                IsOpen = false;
            }

            public void ShowSending()
            {
                SendingShown = true;
            }

            public void ShowAnswer(bool accepted, string reason)
            {
                Answers.Add(accepted ? "accepted" : "rejected:" + reason);
            }

            public void Confirm()
            {
                Confirmed?.Invoke(new LoadoutChoice(Weapons.WeaponIds, Abilities?.AbilityIds));
            }
        }
    }
}
