using System.Collections.Generic;
using System.Text;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦でプレイヤーが選んだロードアウトを、サーバーが武器の一覧と既定のロードアウトのルールで検査し、次の出現から配るか。
    /// 試合の開始時と同じ <see cref="NetTeamDeathmatch.TryCreateLoadout(LoadoutDefinition, WeaponCatalogAsset, out ServerLoadout, out string)"/> で読み、
    /// 出現は <see cref="NetTeamDeathmatch.ArmForSpawn(ServerLoadout, GameRules, NetPlayerWeapon, int)"/> を、
    /// Spawn していない <see cref="NetPlayerWeapon"/> (サーバー側の状態だけを持つ) で確かめる。
    /// </summary>
    public sealed class NetLoadoutChoiceTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-00000000000a";
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";
        private const string _smgId = "00000000-0000-4000-8000-000000000004";

        // どこにも無い Id
        private const string _shotgunId = "00000000-0000-4000-8000-000000000003";

        private const int _smgDamage = 18;
        private const int _playerId = 1;
        private const int _otherPlayerId = 2;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        private WeaponDefinition Definition(string assetName, string id, WeaponCategory category, int damage)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(definition);
            definition.name = assetName;
            definition.Id = id;
            definition.DisplayName = assetName;
            definition.Category = category;
            definition.Damage = damage;
            return definition;
        }

        private WeaponDefinition Rifle()
        {
            return Definition("Rifle", _rifleId, WeaponCategory.AssaultRifle, 25);
        }

        private WeaponDefinition Handgun()
        {
            return Definition("Handgun", _handgunId, WeaponCategory.Handgun, 34);
        }

        private WeaponDefinition Smg()
        {
            return Definition("Smg", _smgId, WeaponCategory.SubmachineGun, _smgDamage);
        }

        /// <summary>メインに AssaultRifle か SubmachineGun、サブに Handgun。既定は rifle と handgun。</summary>
        private LoadoutDefinition LoadoutAsset(WeaponDefinition rifle, WeaponDefinition handgun)
        {
            var loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            _created.Add(loadout);
            loadout.name = "TestLoadout";
            loadout.SetSlots(new[]
            {
                new LoadoutSlotDefinition(new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun }, rifle),
                new LoadoutSlotDefinition(new[] { WeaponCategory.Handgun }, handgun),
            });
            return loadout;
        }

        private WeaponCatalogAsset CatalogAsset(params WeaponDefinition[] weapons)
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            _created.Add(catalog);
            catalog.name = "TestCatalog";
            catalog.SetWeapons(weapons);
            return catalog;
        }

        /// <summary>既定は rifle と handgun、一覧は rifle・handgun・smg から選べる試合のロードアウト。</summary>
        private ServerLoadout ChoiceLoadout()
        {
            var rifle = Rifle();
            var handgun = Handgun();
            Assert.That(
                NetTeamDeathmatch.TryCreateLoadout(LoadoutAsset(rifle, handgun), CatalogAsset(rifle, handgun, Smg()), out var loadout, out var error),
                Is.True, error);
            return loadout;
        }

        private NetPlayerWeapon UnequippedWeapon()
        {
            var go = new GameObject("TestNetPlayer");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            return go.AddComponent<NetPlayerWeapon>();
        }

        /// <summary>出現したときと同じく、playerId に配る武器を持たせたプレイヤー。</summary>
        private NetPlayerWeapon SpawnedWeapon(ServerLoadout loadout, int playerId)
        {
            var weapon = UnequippedWeapon();
            NetTeamDeathmatch.ArmForSpawn(loadout, GameRules.Default, weapon, playerId);
            return weapon;
        }

        [Test]
        public void TheMatchReadsTheCatalogItLetsPlayersChooseFrom()
        {
            var loadout = ChoiceLoadout();

            Assert.That(loadout.Catalog, Is.Not.Null);
            Assert.That(loadout.Catalog.Count, Is.EqualTo(3));
            Assert.That(loadout.Weapons[0].Id, Is.EqualTo(_rifleId), "the default loadout stays as it is");
        }

        [Test]
        public void WithoutACatalogNobodyCanChoose()
        {
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(LoadoutAsset(Rifle(), Handgun()), out var loadout, out var error), Is.True, error);

            Assert.That(loadout.Catalog, Is.Null);
            Assert.That(loadout.TryChoose(_playerId, new[] { _rifleId, _handgunId }, out var reason), Is.False);
            Assert.That(reason, Does.Contain("no weapon catalog"));
        }

        [Test]
        public void RefusesToStartWithAnEmptyCatalog()
        {
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(LoadoutAsset(Rifle(), Handgun()), CatalogAsset(), out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("TestCatalog"));
        }

        [Test]
        public void RefusesToStartWithABrokenCatalogWeapon()
        {
            var broken = Smg();
            broken.name = "BrokenSmg";
            broken.MagazineSize = 0;

            var rifle = Rifle();
            var handgun = Handgun();
            Assert.That(
                NetTeamDeathmatch.TryCreateLoadout(LoadoutAsset(rifle, handgun), CatalogAsset(rifle, handgun, broken), out var loadout, out var error),
                Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("BrokenSmg"));
        }

        [Test]
        public void AnAcceptedChoiceIsGivenAtTheNextSpawn()
        {
            var loadout = ChoiceLoadout();
            var weapon = SpawnedWeapon(loadout, _playerId);
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_rifleId));

            Assert.That(loadout.TryChoose(_playerId, new[] { _smgId, _handgunId }, out var reason), Is.True, reason);

            // 選んだだけでは今の武器は変わらない
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_rifleId));
            Assert.That(weapon.EquippedWeaponIds, Is.EqualTo(new[] { _rifleId, _handgunId }));

            NetTeamDeathmatch.ArmForSpawn(loadout, GameRules.Default, weapon, _playerId);

            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_smgId));
            Assert.That(weapon.EquippedWeaponIds, Is.EqualTo(new[] { _smgId, _handgunId }));

            // 射撃は選んだ武器の値で判定する
            Assert.That(NetTeamDeathmatch.CheckWeaponShot(loadout, _playerId, weapon, _smgId, 0d, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Damage, Is.EqualTo(_smgDamage));
            Assert.That(NetTeamDeathmatch.CheckWeaponShot(loadout, _playerId, weapon, _rifleId, 1d, out _), Is.EqualTo(ShotRejection.WeaponMismatch));
        }

        [Test]
        public void AChoiceOnlyChangesThatPlayersWeapons()
        {
            var loadout = ChoiceLoadout();
            Assert.That(loadout.TryChoose(_playerId, new[] { _smgId, _handgunId }, out var reason), Is.True, reason);

            var other = SpawnedWeapon(loadout, _otherPlayerId);

            Assert.That(other.CurrentSpec.Id, Is.EqualTo(_rifleId));
            Assert.That(other.EquippedWeaponIds, Is.EqualTo(new[] { _rifleId, _handgunId }));
        }

        [Test]
        public void TwoHandgunsAreRejectedAndTheWeaponsDoNotChange()
        {
            var loadout = ChoiceLoadout();

            Assert.That(loadout.TryChoose(_playerId, new[] { _handgunId, _handgunId }, out var reason), Is.False);
            Assert.That(reason, Does.Contain("slot 0 does not accept Handgun"));

            var weapon = SpawnedWeapon(loadout, _playerId);
            Assert.That(weapon.EquippedWeaponIds, Is.EqualTo(new[] { _rifleId, _handgunId }));
        }

        [Test]
        public void ARejectedChoiceKeepsTheEarlierAcceptedOne()
        {
            var loadout = ChoiceLoadout();
            Assert.That(loadout.TryChoose(_playerId, new[] { _smgId, _handgunId }, out var reason), Is.True, reason);
            Assert.That(loadout.TryChoose(_playerId, new[] { _shotgunId, _handgunId }, out _), Is.False);

            var weapon = SpawnedWeapon(loadout, _playerId);

            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_smgId));
        }

        [Test]
        public void TheChoiceIsKeptForTheNextMatch()
        {
            var loadout = ChoiceLoadout();
            Assert.That(loadout.TryChoose(_playerId, new[] { _smgId, _handgunId }, out var reason), Is.True, reason);

            // 次の試合の開始 (NetTeamDeathmatch.HandleRestarted) と同じく連射の制限だけを捨てる
            loadout.ResetBudgets();
            var weapon = SpawnedWeapon(loadout, _playerId);

            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_smgId));
        }

        [Test]
        public void TheClientOnlySendsIdsThatCanBeSynced()
        {
            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(new[] { _rifleId, _handgunId }), Is.Null);

            // ルールに合うかはサーバーが決めるので、同じ武器 2 本でも送る
            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(new[] { _handgunId, _handgunId }), Is.Null);

            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(null), Is.Not.Null);
            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(new string[0]), Is.Not.Null);
            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(new[] { _rifleId, string.Empty }), Does.Contain("slot 1"));
            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(new[] { _rifleId, null }), Does.Contain("slot 1"));
            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(new[] { new string('a', NetPlayerWeapon.MaxWeaponIdBytes + 1) }), Does.Contain("slot 0"));
            Assert.That(NetPlayerWeapon.CheckLoadoutRequest(new string[NetPlayerWeapon.MaxWeaponCount + 1]), Does.Contain("at most"));
        }

        [Test]
        public void TheServerIgnoresAChoiceFromSomeoneElse()
        {
            var weapon = UnequippedWeapon();
            var stranger = weapon.OwnerClientId + 1;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("ignored loadout from client " + stranger));
            Assert.That(weapon.ServerHandleLoadoutRequest(stranger, new[] { _rifleId, _handgunId }, out var reason), Is.False);
            Assert.That(reason, Is.Null, "no answer goes back to someone who does not own the player");
        }

        [Test]
        public void TheServerRejectsAChoiceWithoutARunningMatch()
        {
            Assume.That(NetTeamDeathmatch.Current, Is.Null);
            var weapon = UnequippedWeapon();

            LogAssert.Expect(LogType.Warning, LoadoutChoiceLog.Rejected(weapon.OwnerClientId, "no match is running"));
            Assert.That(weapon.ServerHandleLoadoutRequest(weapon.OwnerClientId, new[] { _rifleId, _handgunId }, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("no match is running"));
        }

        [Test]
        public void TheAnswerIsCutToFitWithoutSplittingACharacter()
        {
            Assert.That(NetPlayerWeapon.FitAnswer(null), Is.Empty);
            Assert.That(NetPlayerWeapon.FitAnswer("slot 0 does not accept Handgun"), Is.EqualTo("slot 0 does not accept Handgun"));

            // 3 バイトの文字で上限を越える
            var max = NetPlayerWeapon.MaxAnswerBytes;
            var reason = new string('あ', max);
            var text = NetPlayerWeapon.FitAnswer(reason);

            Assert.That(reason.StartsWith(text, System.StringComparison.Ordinal), Is.True);
            Assert.That(Encoding.UTF8.GetByteCount(text), Is.LessThanOrEqualTo(max));
            Assert.That(Encoding.UTF8.GetByteCount(text), Is.GreaterThan(max - 3));

            // サロゲートペア (4 バイト) を半分で切らない
            var emoji = new string('a', max - 2) + "\U0001F600";
            Assert.That(NetPlayerWeapon.FitAnswer(emoji), Is.EqualTo(new string('a', max - 2)));
        }

        // RPC で送るときと同じく INetworkSerializable で書いて読み戻す
        private static NetLoadoutRequest RoundTrip(NetLoadoutRequest value)
        {
            using (var writer = new FastBufferWriter(64, Allocator.Temp, 64 * 1024))
            {
                writer.WriteNetworkSerializable(value);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out NetLoadoutRequest result);
                    Assert.That(reader.Position, Is.EqualTo(writer.Length), "every written byte should be read back");
                    return result;
                }
            }
        }

        [Test]
        public void TheRequestCarriesTheIdsInSlotOrder()
        {
            Assert.That(RoundTrip(new NetLoadoutRequest(new[] { _smgId, _handgunId })).WeaponIds, Is.EqualTo(new[] { _smgId, _handgunId }));
            Assert.That(RoundTrip(new NetLoadoutRequest(new[] { _handgunId, null })).WeaponIds, Is.EqualTo(new[] { _handgunId, string.Empty }));
            Assert.That(RoundTrip(new NetLoadoutRequest(null)).WeaponIds, Is.Empty);
            Assert.That(RoundTrip(default).WeaponIds, Is.Empty);
        }

        [Test]
        public void TheServerRefusesToReadTooManyIds()
        {
            using (var writer = new FastBufferWriter(64, Allocator.Temp))
            {
                // 本数だけを偽って書いた要求
                var count = NetPlayerWeapon.MaxWeaponCount + 1;
                writer.WriteValueSafe(count);
                var reader = new FastBufferReader(writer, Allocator.Temp);
                try
                {
                    Assert.Throws<System.InvalidOperationException>(() => reader.ReadNetworkSerializable(out NetLoadoutRequest _));
                }
                finally
                {
                    reader.Dispose();
                }
            }
        }

        [Test]
        public void SwitchingWalksTheWeaponsThePlayerHolds()
        {
            var ids = new[] { _smgId, _handgunId };

            Assert.That(NetPlayerWeapon.NeighbourId(ids, _smgId, 1), Is.EqualTo(_handgunId));
            Assert.That(NetPlayerWeapon.NeighbourId(ids, _handgunId, 1), Is.EqualTo(_smgId));
            Assert.That(NetPlayerWeapon.NeighbourId(ids, _smgId, -1), Is.EqualTo(_handgunId));
            Assert.That(NetPlayerWeapon.NeighbourId(ids, _rifleId, 1), Is.Null, "not held");
            Assert.That(NetPlayerWeapon.NeighbourId(new string[0], _smgId, 1), Is.Null, "nothing synced yet");
            Assert.That(NetPlayerWeapon.NeighbourId(null, _smgId, 1), Is.Null);
        }

        [Test]
        public void TheClientFindsAChosenWeaponInTheCatalog()
        {
            var rifle = Rifle();
            var handgun = Handgun();
            var loadout = LoadoutAsset(rifle, handgun);
            var catalog = CatalogAsset(rifle, handgun, Smg());

            Assert.That(NetTeamDeathmatch.TryFindWeaponSpec(loadout, catalog, _rifleId, out var fromLoadout, out var error), Is.True, error);
            Assert.That(fromLoadout.Id, Is.EqualTo(_rifleId));
            Assert.That(NetTeamDeathmatch.TryFindWeaponSpec(loadout, catalog, _smgId, out var fromCatalog, out error), Is.True, error);
            Assert.That(fromCatalog.Damage, Is.EqualTo(_smgDamage));

            Assert.That(NetTeamDeathmatch.TryFindWeaponSpec(loadout, catalog, _shotgunId, out var missing, out error), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(error, Does.Contain("TestLoadout").And.Contain("TestCatalog"));

            // 一覧が無い試合では、既定のロードアウトの武器しか引けない
            Assert.That(NetTeamDeathmatch.TryFindWeaponSpec(loadout, null, _smgId, out _, out error), Is.False);
            Assert.That(NetTeamDeathmatch.TryFindWeaponSpec(null, null, _rifleId, out _, out error), Is.False);
            Assert.That(error, Does.Contain("without the match's loadout"));
        }
    }
}
