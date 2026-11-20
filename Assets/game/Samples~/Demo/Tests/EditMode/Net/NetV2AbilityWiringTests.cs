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
    // NetV2Client と同じ組み方 (NetV2ClientParts.Create に能力のカタログと NetV2LoadoutChooser を渡す) で、
    // 保存した選択を送る → サーバーが受け入れる → Ability1・Ability2 の枠が替わる → 置ける、を通しで確かめる。
    // 選んでいない種類をサーバーが NotEquipped で断ることは、dotnet の ServerDeployablesTests で確かめる
    public sealed class NetV2AbilityWiringTests
    {
        private const string StoreKey = "Shooter.Tests.NetV2AbilityWiring";
        private const string BlocksKey = "Shooter.Tests.NetV2AbilityWiring.Blocks";
        private const string VoiceKey = "Shooter.Tests.NetV2AbilityWiring.Voice";
        private const string RifleId = "00000000-0000-4000-8000-0000000000a1";
        private const string HandgunId = "00000000-0000-4000-8000-0000000000a2";
        private const string ShieldId = "deploy-shield";
        private const string BeaconId = "deploy-beacon";
        private static readonly StanceGeometry _stance = new StanceGeometry(2f, 0f, 0.6f, 1.2f, 0.5f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _logs = new List<string>();
        private readonly List<string[]> _abilityRequests = new List<string[]>();
        private readonly List<string> _deployRequests = new List<string>();
        private LoadoutPreferenceStore _store;
        private GameContent _content;
        private LoadoutDefinition _loadout;
        private AbilityCatalogAsset _abilities;
        private ShieldDeployDefinition _shield;
        private BeaconDeployDefinition _beacon;
        private NetV2LoadoutChooser _chooser;
        private MatchClient _client;
        private NetV2LocalPlayer _local;
        private List<INetV2ClientPart> _parts;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _abilityRequests.Clear();
            _deployRequests.Clear();
            _store = new LoadoutPreferenceStore(StoreKey);
            _store.Delete();
            new BlockListStore(BlocksKey).Delete();

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

            // ロードアウトは盾だけを持ち、ビーコンはカタログにしか無い
            _loadout.SetAbilities(new AbilityDefinition[] { _shield });
            _abilities = Create<AbilityCatalogAsset>("TestAbilities");
            _abilities.SetAbilities(new AbilityDefinition[] { _shield, _beacon });

            _content = Create<GameContent>("TestContent");
            _content.Loadout = _loadout;
            _content.WeaponCatalog = weapons;
            _content.AbilityCatalog = _abilities;
            GameContent.Use(_content);

            _chooser = new NetV2LoadoutChooser(new NetV2Weapons(), Send, null, () => string.Empty, _store);
            _client = new MatchClient(new SilentTransport(), "0.2.0", RejoinKey.NewDeviceKey(), "tester", _logs.Add);
            _local = NetV2LocalPlayer.Create(_stance, MotorSpec.Default, () => true, Vector3.zero, 0f);
            _created.Add(_local.gameObject);
        }

        [TearDown]
        public void TearDown()
        {
            DisposeParts();
            _chooser.Dispose();
            _client.Dispose();
            GameContent.Use(null);
            _store.Delete();
            new BlockListStore(BlocksKey).Delete();
            GameSignals.BlockControl.Clear();
            GameSignals.MapVote.Clear();
            GameSignals.MapVoteSender.Clear();
            GameSignals.Chat.Clear();
            GameSignals.ChatSender.Clear();
            GameSignals.Minimap.Clear();
            GameSignals.Spectate.Clear();
            GameSignals.Voice.Clear();
            GameSignals.Streak.Clear();
            GameSignals.KillCam.Clear();
            GameSignals.ReportSender.Clear();
            GameSignals.VoteKick.Clear();
            GameSignals.VoteKickSender.Clear();
            GameSignals.Wallet.Clear();
            GameSignals.BuySender.Clear();
            GameSignals.NetStats.Clear();
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
        public void RightAfterConnectingTheSlotsAreTheLoadoutsAbilities()
        {
            _parts = NetV2ClientParts.Create(Context());

            var input = Input();
            Assert.That(input.Slots[0], Is.SameAs(_shield));
            Assert.That(input.Slots[1], Is.Null);
        }

        [Test]
        public void TheSavedBeaconIsSentAndOnceAcceptedAbility1PlacesIt()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { BeaconId, AbilitySelection.None }));
            _parts = NetV2ClientParts.Create(Context());
            var input = Input();

            _chooser.SendSaved();

            Assert.That(_abilityRequests, Has.Count.EqualTo(1));
            Assert.That(_abilityRequests[0], Is.EqualTo(new[] { BeaconId, AbilitySelection.None }));
            Assert.That(input.Slots[0], Is.SameAs(_shield), "the slots wait for the server's answer");

            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(input.Slots[0], Is.SameAs(_beacon));
            Assert.That(input.Slots[1], Is.Null, "Ability2 was left empty");
            Assert.That(_logs, Does.Contain("[Deploy] net slots kinds=deploy-beacon,-"));

            // 置く物の部品はカタログのビーコンも知っているので、ロードアウトに無くても置く要求まで進む
            Deployables().Deploy(input.Slots[0].Id, _local.transform, _local.EyePosition, _local.AimDirection);

            Assert.That(_logs, Does.Not.Contain(ClientDeployLog.UnknownType(BeaconId)));
            Assert.That(_logs.Any(line => line.StartsWith("[Deploy] net request kind=deploy-beacon", StringComparison.Ordinal)), Is.True, string.Join("\n", _logs));
        }

        [Test]
        public void ChoosingNoneInBothSlotsLeavesNothingToPlace()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { AbilitySelection.None, AbilitySelection.None }));
            _parts = NetV2ClientParts.Create(Context());

            _chooser.SendSaved();
            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(Input().Slots.All(slot => slot == null), Is.True);
            Assert.That(_logs, Does.Contain("[Deploy] net slots kinds=-,-"));
        }

        [Test]
        public void ARejectedChoiceKeepsTheLoadoutsSlots()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { BeaconId, AbilitySelection.None }));
            _parts = NetV2ClientParts.Create(Context());

            _chooser.SendSaved();
            _chooser.Answer(new LoadoutAnswerMessage { Accepted = false, Reason = "ability slot 0 names an ability the rules do not offer" });

            Assert.That(Input().Slots[0], Is.SameAs(_shield));
            Assert.That(_logs.Any(line => line.StartsWith("[Deploy] net slots", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void ATypeOutsideTheLoadoutAndTheCatalogIsNotRequested()
        {
            _parts = NetV2ClientParts.Create(Context());

            Assert.That(Deployables().Deploy("deploy-missing", _local.transform, _local.EyePosition, _local.AimDirection), Is.False);

            Assert.That(_logs, Does.Contain(ClientDeployLog.UnknownType("deploy-missing")));
            Assert.That(_deployRequests, Is.Empty);
        }

        [Test]
        public void WithoutTheChooserTheSlotsStayTheLoadouts()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { BeaconId, AbilitySelection.None }));
            var context = Context();
            context.Loadout = null;
            _parts = NetV2ClientParts.Create(context);

            _chooser.SendSaved();
            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(Input().Slots[0], Is.SameAs(_shield));
        }

        [Test]
        public void DisposedPartsNoLongerFollowTheChooser()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { BeaconId, AbilitySelection.None }));
            _parts = NetV2ClientParts.Create(Context());
            var input = Input();
            DisposeParts();

            _chooser.SendSaved();
            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(input.Slots[0], Is.SameAs(_shield));
            Assert.That(_logs.Any(line => line.StartsWith("[Deploy] net slots", StringComparison.Ordinal)), Is.False);
        }

        // 送ったときは読めたカタログが答えまでに壊れたら、サーバーの持たせた種類と食い違う枠を残さないよう空にする
        [Test]
        public void ACatalogThatBreaksBeforeTheAnswerEmptiesTheSlots()
        {
            _store.Save(new LoadoutChoice(new[] { RifleId, HandgunId }, new[] { BeaconId, AbilitySelection.None }));
            _parts = NetV2ClientParts.Create(Context());
            _chooser.SendSaved();

            _abilities.SetAbilities(new AbilityDefinition[] { _shield, _beacon, null });
            _chooser.Answer(new LoadoutAnswerMessage { Accepted = true });

            Assert.That(Input().Slots.All(slot => slot == null), Is.True);
            Assert.That(
                _logs,
                Does.Contain("[Deploy] net slots emptied: the accepted abilities cannot be looked up in the ability catalog: ability catalog 'TestAbilities' ability #2 is empty"));
            Assert.That(_logs, Does.Contain("[Deploy] net slots kinds=-,-"));
        }

        [Test]
        public void TheDeployDefinitionsAreTheLoadoutsFollowedByTheCatalogs()
        {
            var grapple = Create<GrappleAbilityDefinition>("GrappleAsset");
            grapple.Id = "grapple";
            _abilities.SetAbilities(new AbilityDefinition[] { _beacon, grapple, _shield });

            var definitions = NetV2ClientParts.DeployDefinitionsOf(new AbilityDefinition[] { _shield, null }, _abilities);

            Assert.That(definitions, Is.EqualTo(new DeployAbilityDefinition[] { _shield, _beacon, _shield }), "abilities that place nothing are left out");
            Assert.That(NetV2ClientParts.DeployDefinitionsOf(null, null), Is.Empty);
            Assert.That(NetV2ClientParts.DeployDefinitionsOf(new AbilityDefinition[] { _shield }, null), Is.EqualTo(new DeployAbilityDefinition[] { _shield }));
        }

        private NetV2ClientPartsContext Context()
        {
            return new NetV2ClientPartsContext(_client, Send, _logs.Add)
            {
                LocalPlayer = () => _local,
                VoiceSettings = new VoiceSettingsStore(VoiceKey),
                PushToTalkHeld = () => false,
                Blocks = new BlockListStore(BlocksKey),
                Melee = MeleeSpec.Default,
                Streaks = StreakSpec.Default,
                Abilities = _loadout.Abilities,
                AbilityCatalog = _abilities,
                Loadout = _chooser,
            };
        }

        private NetV2DeployInputPart Input()
        {
            return _parts.OfType<NetV2DeployInputPart>().Single();
        }

        private NetV2DeployablesPart Deployables()
        {
            return _parts.OfType<NetV2DeployablesPart>().Single();
        }

        private void DisposeParts()
        {
            if (_parts == null)
            {
                return;
            }

            foreach (var part in _parts)
            {
                part.Dispose();
            }

            _parts = null;
        }

        // 要求の入れ物は送るたびに使い回されるので、送った時点の中身を写す
        private bool Send(IMessage message)
        {
            if (message is LoadoutRequestMessage request)
            {
                var ids = new string[request.AbilityIds.Count];
                for (var i = 0; i < ids.Length; i++)
                {
                    ids[i] = request.AbilityIds[i];
                }

                _abilityRequests.Add(ids);
            }
            else if (message is DeployRequestMessage deploy)
            {
                _deployRequests.Add(deploy.TypeId);
            }

            return true;
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

        private sealed class SilentTransport : IMatchTransport
        {
            public bool IsActive => false;

            public void Listen(string address, ushort port)
            {
                throw new InvalidOperationException("a client transport does not listen");
            }

            public void Connect(string address, ushort port)
            {
            }

            public void Disconnect(ConnectionId connection, string reason)
            {
            }

            public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
            {
            }

            public int GetMaxPayloadSize(TransportChannel channel)
            {
                return MessageWriter.DefaultMaxLength;
            }

            public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
            {
                milliseconds = 0;
                return false;
            }

            public bool TryReceive(out TransportEvent transportEvent)
            {
                transportEvent = default;
                return false;
            }

            public void Shutdown(string reason)
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
