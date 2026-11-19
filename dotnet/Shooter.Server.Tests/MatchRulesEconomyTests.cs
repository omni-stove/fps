using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server.Tests
{
    public sealed class MatchRulesEconomyTests
    {
        private const float _tickSeconds = 1f / 60f;
        private const string _carbineId = "0d6f5c1e-2b7a-4c39-9e84-5a1f3b7c2d90";
        private const string _ghostId = "7a3e9b52-1c4d-4f86-b0e2-6d9c8a5f1e37";
        private const string _fragId = "ff94f232-2647-4b77-a077-5693d8f954d1";
        private const string _sideArmId = "3b8d2f61-5e4a-4c07-a9d3-8e1b6c4f2a75";
        private const string _spareId = "9c2e7a14-6f3b-4d58-8a1e-2b5d9f7c3e60";
        private static readonly DateTime _time = new DateTime(2026, 11, 19, 9, 0, 0, DateTimeKind.Utc);

        private readonly List<int> _rearmed = new List<int>();
        private readonly Dictionary<int, string> _spawnedWith = new Dictionary<int, string>();
        private StringWriter _log;
        private RecordingTransport _transport;
        private ServerMatch _match;
        private MatchRulesModule _module;
        private long _tick;

        [SetUp]
        public void SetUp()
        {
            _log = new StringWriter();
            _transport = new RecordingTransport();
            _rearmed.Clear();
            _spawnedWith.Clear();
            _tick = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _module?.Stop();
            _transport.Dispose();
            _log.Dispose();
        }

        [Test]
        public void KillsAndRoundsPayTheAmountsOfTheMatchSettings()
        {
            Start();
            Join(1);
            Join(2);
            Step();
            Assert.That(Money(1), Is.EqualTo(800), "everyone starts with the start money");
            Assert.That(Money(2), Is.EqualTo(800));

            Advance(1.1f);
            Assert.That(_module.ApplyDamage(1, 2, 100), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(Money(1), Is.EqualTo(1100), "a kill pays 300 at once");
            Step();

            Assert.That(_module.SearchAndDestroy.GetRoundWins(Side.Alpha), Is.EqualTo(1));
            Assert.That(Money(1), Is.EqualTo(4100), "the round win pays 3000");
            Assert.That(Money(2), Is.EqualTo(2200), "the first loss pays 1400");

            Advance(1.1f);
            _module.ApplyDamage(1, 2, 100);
            Step();

            Assert.That(Money(1), Is.EqualTo(7400));
            Assert.That(Money(2), Is.EqualTo(4100), "the second loss in a row pays 1400 + 500");
            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Economy] started players=0 start=800 max=16000 items=6 resetOnSwap=yes"));
            Assert.That(log, Does.Contain("[Economy] round=1 buy=open"));
            Assert.That(log, Does.Contain("[Economy] round=1 buy=closed"));
            Assert.That(log, Does.Contain("[Economy] player=1 kill victim=2 +300 money=1100"));
            Assert.That(log, Does.Contain("[Economy] player=1 side=0 round won +3000 money=4100 lossStreak=0"));
            Assert.That(log, Does.Contain("[Economy] player=2 side=1 round lost +1400 money=2200 lossStreak=1"));
            Assert.That(log, Does.Contain("[Economy] player=2 side=1 round lost +1900 money=4100 lossStreak=2"));
        }

        [Test]
        public void OnlyTheOwnerSeesTheirMoneyInTheSnapshot()
        {
            Start(startMoney: 1250);
            Join(1);
            Join(2);
            Step();

            Assert.That(OwnerMoney(1), Is.EqualTo(1250));
            var snapshot = new SnapshotMessage();
            _module.Contribute(snapshot, null);
            Assert.That(snapshot.Owner.Money, Is.Zero, "a snapshot with no receiver carries no money");

            _module.ServerBuy(1, _carbineId);
            Assert.That(OwnerMoney(1), Is.EqualTo(1250 - 1000));
            Assert.That(OwnerMoney(2), Is.EqualTo(1250), "another player's purchase does not show in my wallet");
        }

        [Test]
        public void BuyingInThePreparationTakesThePriceAndRearmsOnTheNextTick()
        {
            Start(startMoney: 2500);
            Join(1);
            Join(2);
            Step();
            Assert.That(_module.Economy.IsBuyTime, Is.True);

            Assert.That(_module.ServerBuy(1, _carbineId), Is.EqualTo(PurchaseVerdict.Bought));

            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(1500));
            Assert.That(_module.WeaponsFor(1)[0].Id, Is.EqualTo(_carbineId), "the bought weapon takes its slot");
            Assert.That(_module.WeaponsFor(1)[1].Id, Is.EqualTo(TestRules.HandgunId), "the other slot keeps the loadout weapon");
            Assert.That(_module.WeaponsFor(2)[0].Id, Is.EqualTo(TestRules.RifleId));
            Assert.That(_rearmed, Is.Empty, "the weapons are not swapped in the middle of the request");
            Assert.That(_log.ToString(), Does.Contain(
                "[Economy] player=1 bought item=" + _carbineId + " kind=Weapon slot=0 price=1000 money=1500"));

            Step();

            Assert.That(_rearmed, Has.Count.EqualTo(1));
            Assert.That(_rearmed[0], Is.EqualTo(1));
            Assert.That(_log.ToString(), Does.Contain("[Economy] player=1 equipped weapons=2"));
        }

        [Test]
        public void ABuyRequestMessageBuysForItsSender()
        {
            Start(startMoney: 2500);
            Join(1);
            Join(2);
            Step();

            _module.OnMessage(new ConnectionId(2), new BuyRequestMessage { ItemId = _carbineId });

            Assert.That(_module.Economy.MoneyOf(2), Is.EqualTo(1500));
            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(2500));
            Assert.That(_module.WeaponsFor(2)[0].Id, Is.EqualTo(_carbineId));
        }

        [Test]
        public void ABuyRequestIsAnsweredToItsSenderOnly()
        {
            Start(startMoney: 2500);
            Join(1);
            Join(2);
            Step();

            _module.OnMessage(new ConnectionId(2), new BuyRequestMessage { ItemId = _carbineId });

            var answers = BuyAnswers(2);
            Assert.That(answers, Has.Count.EqualTo(1));
            Assert.That(answers[0].ItemId, Is.EqualTo(_carbineId));
            Assert.That(answers[0].Verdict, Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(BuyAnswers(1), Is.Empty, "the other player is not told about the purchase");
        }

        [TestCase(_carbineId, 800, PurchaseVerdict.NotEnoughMoney)]
        [TestCase("not-in-the-list", 5000, PurchaseVerdict.UnknownItem)]
        [TestCase(TestRules.RifleId, 5000, PurchaseVerdict.CannotCarry)]
        public void ARefusedBuyRequestIsAnsweredWithItsReason(string itemId, int startMoney, PurchaseVerdict expected)
        {
            Start(startMoney: startMoney);
            Join(1);
            Join(2);
            Step();

            _module.OnMessage(new ConnectionId(1), new BuyRequestMessage { ItemId = itemId });

            var answers = BuyAnswers(1);
            Assert.That(answers, Has.Count.EqualTo(1));
            Assert.That(answers[0].ItemId, Is.EqualTo(itemId), "the id of the request comes back even when it is not in the list");
            Assert.That(answers[0].Verdict, Is.EqualTo(expected));
            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(startMoney));
        }

        [Test]
        public void ABuyRequestWithoutAnEconomyIsAnsweredAsNotTheBuyTime()
        {
            Start(buyList: false);
            Join(1);
            Join(2);
            Step();

            _module.OnMessage(new ConnectionId(1), new BuyRequestMessage { ItemId = _carbineId });

            var answers = BuyAnswers(1);
            Assert.That(answers, Has.Count.EqualTo(1));
            Assert.That(answers[0].Verdict, Is.EqualTo(PurchaseVerdict.NotBuyTime));
        }

        [TestCase(_carbineId, 800, PurchaseVerdict.NotEnoughMoney)]
        [TestCase("not-in-the-list", 5000, PurchaseVerdict.UnknownItem)]
        [TestCase(TestRules.RifleId, 5000, PurchaseVerdict.CannotCarry)]
        [TestCase(_fragId, 5000, PurchaseVerdict.CannotCarry)]
        [TestCase(_ghostId, 5000, PurchaseVerdict.CannotCarry)]
        [TestCase(_spareId, 5000, PurchaseVerdict.CannotCarry)]
        [TestCase(_sideArmId, 5000, PurchaseVerdict.CannotCarry)]
        public void ARefusedPurchaseKeepsTheMoneyAndTellsWhy(string itemId, int startMoney, PurchaseVerdict expected)
        {
            Start(startMoney: startMoney);
            Join(1);
            Join(2);
            Step();

            Assert.That(_module.ServerBuy(1, itemId), Is.EqualTo(expected));

            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(startMoney));
            Assert.That(_module.WeaponsFor(1)[0].Id, Is.EqualTo(TestRules.RifleId));
            var logged = expected == PurchaseVerdict.UnknownItem ? "-" : itemId;
            Assert.That(_log.ToString(), Does.Contain(
                "[Economy] player=1 refused item=" + logged + " verdict=" + expected + " money=" + startMoney));
            Step();
            Assert.That(_rearmed, Is.Empty);
        }

        [Test]
        public void TheSameBoughtWeaponCannotBeBoughtTwice()
        {
            Start(startMoney: 5000);
            Join(1);
            Join(2);
            Step();
            _module.ServerBuy(1, _carbineId);

            Assert.That(_module.ServerBuy(1, _carbineId), Is.EqualTo(PurchaseVerdict.CannotCarry));
            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(4000));
        }

        [Test]
        public void PurchasesAreRefusedOutsideThePreparation()
        {
            Start(startMoney: 5000);
            Join(1);
            Assert.That(_module.ServerBuy(1, _carbineId), Is.EqualTo(PurchaseVerdict.NotBuyTime), "the first round has not started preparing");

            Join(2);
            Step();
            Advance(1.1f);
            Assert.That(_module.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(_module.ServerBuy(1, _carbineId), Is.EqualTo(PurchaseVerdict.NotBuyTime), "the round is live");
            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(5000));

            _module.ApplyDamage(1, 2, 100);
            Step();
            Assert.That(_module.SearchAndDestroy.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(_module.ServerBuy(1, _carbineId), Is.EqualTo(PurchaseVerdict.Bought), "the next preparation opens the buy time again");
            Assert.That(_log.ToString(), Does.Contain("[Economy] round=2 buy=open"));
        }

        [Test]
        public void ABoughtWeaponIsCarriedToTheNextSpawnUntilItsOwnerFalls()
        {
            Start(startMoney: 2500);
            Join(1);
            Join(2);
            Step();
            _module.ServerBuy(1, _carbineId);
            Advance(1.1f);

            _module.ApplyDamage(1, 2, 100);
            Step();

            Assert.That(_module.SearchAndDestroy.RoundNumber, Is.EqualTo(2));
            Assert.That(_spawnedWith[1], Is.EqualTo(_carbineId), "the survivor spawns into round 2 with the bought weapon");
            Assert.That(_spawnedWith[2], Is.EqualTo(TestRules.RifleId));

            Advance(1.1f);
            _module.ApplyDamage(2, 1, 100);

            Assert.That(_module.Economy.WeaponsOf(1), Is.Empty);
            Assert.That(_module.WeaponsFor(1)[0].Id, Is.EqualTo(TestRules.RifleId), "falling loses the bought weapon");
            Assert.That(_log.ToString(), Does.Contain("[Economy] player=1 lost the bought weapons"));

            Step();
            Assert.That(_spawnedWith[1], Is.EqualTo(TestRules.RifleId), "round 3 starts with the loadout weapon again");
        }

        [Test]
        public void TheNextMatchStartsEveryWalletAgain()
        {
            Start(startMoney: 2500, winsToWin: 1, restartDelaySeconds: 2f);
            Join(1);
            Join(2);
            Step();
            _module.ServerBuy(1, _carbineId);
            Advance(1.1f);
            _module.ApplyDamage(1, 2, 100);
            Step();
            Assert.That(_match.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(_module.Economy.IsBuyTime, Is.False);

            Advance(2.1f);

            Assert.That(_match.State, Is.EqualTo(GameModeState.Playing));
            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(2500));
            Assert.That(_module.Economy.MoneyOf(2), Is.EqualTo(2500));
            Assert.That(_module.Economy.WeaponsOf(1), Is.Empty);
            Assert.That(_module.WeaponsFor(1)[0].Id, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void SwappingSidesResetsTheWalletsWhenTheBuyListSaysSo()
        {
            Start(startMoney: 2500, swapEveryRounds: 1);
            Join(1);
            Join(2);
            Step();
            _module.ServerBuy(1, _carbineId);
            Advance(1.1f);
            _module.ApplyDamage(1, 2, 100);
            Step();

            Assert.That(_module.SearchAndDestroy.Attacker, Is.EqualTo(Side.Bravo));
            Assert.That(_module.Economy.MoneyOf(1), Is.EqualTo(2500));
            Assert.That(_module.Economy.MoneyOf(2), Is.EqualTo(2500));
            Assert.That(_spawnedWith[1], Is.EqualTo(TestRules.RifleId), "the reset also takes the bought weapons");
            Assert.That(_log.ToString(), Does.Contain("[Economy] sides swapped wallets reset"));
        }

        [Test]
        public void AJoinerGetsAWalletAndALeaverLosesIt()
        {
            Start();
            Join(1);
            Join(2);
            Step();
            Join(3);

            Assert.That(_module.Economy.HasPlayer(3), Is.True);
            Assert.That(_module.Economy.SideOf(3), Is.EqualTo(Side.Alpha));
            Assert.That(_module.Economy.MoneyOf(3), Is.EqualTo(800));

            _match.Leave(new ConnectionId(3));
            Step();

            Assert.That(_module.Economy.HasPlayer(3), Is.False);
            Assert.That(_log.ToString(), Does.Contain("[Economy] player=3 left; the wallet is dropped"));
        }

        [Test]
        public void TheStartupLogNamesTheItemsTheServerCannotSell()
        {
            Start();

            var log = _log.ToString();
            Assert.That(log, Does.Contain("[Economy] item=" + _fragId + " kind=Throwable is not sold: the server only hands out bought weapons"));
            Assert.That(log, Does.Contain(
                "[Economy] the weapon '" + _ghostId + "' in the buy list cannot be equipped: not in the loadout or the weapon catalog of rules SearchAndDestroy"));
            Assert.That(log, Does.Contain(
                "[Economy] the weapon '" + _spareId + "' in the buy list cannot be equipped: slot 2 is not one of the 2 loadout slots of rules SearchAndDestroy"));
        }

        [Test]
        public void WithoutABuyListThereIsNoEconomy()
        {
            Start(buyList: false);
            Join(1);
            Join(2);
            Step();

            Assert.That(_module.Economy, Is.Null);
            Assert.That(_log.ToString(), Does.Contain("[Economy] off: no buy list (tuning/economy.json) is beside the rules SearchAndDestroy"));
            Assert.That(_module.ServerBuy(1, _carbineId), Is.EqualTo(PurchaseVerdict.NotBuyTime));
            Assert.That(OwnerMoney(1), Is.Zero);
            Assert.That(_module.WeaponsFor(1)[0].Id, Is.EqualTo(TestRules.RifleId));
        }

        [Test]
        public void ObjectivesWithoutRoundsHaveNoEconomyEvenWithABuyList()
        {
            Start(objective: MatchObjective.Elimination);
            Join(1);
            Join(2);
            Step();

            Assert.That(_module.Economy, Is.Null);
            Assert.That(_log.ToString(), Does.Not.Contain("[Economy]"));
            Assert.That(OwnerMoney(1), Is.Zero);
        }

        private static WeaponSpec Carbine() => new WeaponSpec(
            _carbineId, "weapon.carbine", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.09f, 2f, 22, 90f, DamageFalloff.None);

        // 持たせられる武器 (カービン)、持っている武器 (ライフル)、サーバーが配らない品 (投げ物・一覧に無い武器・枠の外・枠のルールに合わない並び) を並べる。
        // 額は一覧のものを使わないことも確かめるため、試合の設定と違う額にする
        private static EconomyRules BuyList()
        {
            return new EconomyRules(
                new EconomySpec(startMoney: 0, maxMoney: 1000, killReward: 0),
                new BuyCatalog(new[]
                {
                    new BuyItem(_carbineId, BuyItemKind.Weapon, 1000, slot: 0),
                    new BuyItem(TestRules.RifleId, BuyItemKind.Weapon, 2700, slot: 0),
                    new BuyItem(_fragId, BuyItemKind.Throwable, 300),
                    new BuyItem(_ghostId, BuyItemKind.Weapon, 500, slot: 0),
                    new BuyItem(_spareId, BuyItemKind.Weapon, 500, slot: 2),
                    new BuyItem(_sideArmId, BuyItemKind.Weapon, 200, slot: 0),
                }));
        }

        private static SearchAndDestroySpec Spec(int winsToWin, int swapEveryRounds)
        {
            return new SearchAndDestroySpec(
                siteCount: 2,
                roundSeconds: 30f,
                plantSeconds: 1f,
                defuseSeconds: 2f,
                fuseSeconds: 5f,
                winsToWin: winsToWin,
                preparationSeconds: 1f,
                swapEveryRounds: swapEveryRounds,
                bombPickupRadius: 1.5f,
                defuseRadius: 2f);
        }

        private void Start(
            int startMoney = EconomySpec.DefaultStartMoney,
            int winsToWin = 4,
            int swapEveryRounds = 0,
            float restartDelaySeconds = 10f,
            bool buyList = true,
            MatchObjective objective = MatchObjective.SearchAndDestroy)
        {
            var settings = new NetMatchSettings(
                restartDelaySeconds: restartDelaySeconds,
                searchAndDestroy: Spec(winsToWin, swapEveryRounds),
                economy: new EconomySpec(startMoney: startMoney));
            var rules = GameRules.Default
                .WithActions(ActionRules.Default.WithDowned(Downed.Off))
                .WithRespawnMode(RespawnMode.None)
                .WithSpawnProtectionSeconds(0f)
                .WithIdleKickSeconds(0f)
                .WithRegenPerSecond(0f)
                .WithObjective(objective);
            var rifle = TestRules.Rifle();
            var handgun = TestRules.Handgun();
            var file = new NetMatchFile(
                "SearchAndDestroy",
                rules,
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { rifle, handgun },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { rifle, handgun, Carbine(), SideArm(), Spare() }),
                null);
            var data = new ServerRulesData(
                "SearchAndDestroy", file, PingSpec.Default, "memory", null, buyList: buyList ? BuyList() : null, economyPath: buyList ? "memory" : null);
            _match = new ServerMatch(settings.RestartDelaySeconds, _log, new ServerEventWriter(true, TextWriter.Null, _log), () => _time);
            var context = new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                "1.0.0",
                _log,
                TestMaps.Data("NetTest", TestRules.Points(2)),
                _match,
                new ServerMessenger(_transport),
                new StubControl(),
                data);
            var module = new MatchRulesModule();
            module.Start(context);
            module.WeaponsChanged += _rearmed.Add;
            module.PlayerSpawned += id => _spawnedWith[id] = module.WeaponsFor(id)[0].Id;
            _module = module;
        }

        // 武器の一覧には載っているが、前の枠 (アサルトライフルだけ) のルールに合わない拳銃
        private static WeaponSpec SideArm() => new WeaponSpec(
            _sideArmId, "weapon.sideArm", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);

        // 武器の一覧には載っているが、買える品の一覧で 3 つ目の枠 (ロードアウトには 2 つしかない) に置かれた武器
        private static WeaponSpec Spare() => new WeaponSpec(
            _spareId, "weapon.spare", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, DamageFalloff.None);

        private void Join(int id)
        {
            _match.Join(new ConnectionId((ulong)id), "device" + id);
        }

        private int Money(int id)
        {
            return _module.Economy.MoneyOf(id);
        }

        private int OwnerMoney(int id)
        {
            Assert.That(_match.TryGetPlayer(id, out var player), Is.True);
            var snapshot = new SnapshotMessage();
            _module.Contribute(snapshot, player);
            return snapshot.Owner.Money;
        }

        private List<BuyAnswerMessage> BuyAnswers(int id)
        {
            return _transport.Read<BuyAnswerMessage>(id, m => new BuyAnswerMessage { ItemId = m.ItemId, Verdict = m.Verdict });
        }

        private void Step()
        {
            _match.Tick(_tickSeconds);
            _module.Tick(++_tick, _tickSeconds);
        }

        private void Advance(float seconds)
        {
            for (var elapsed = 0f; elapsed < seconds; elapsed += _tickSeconds)
            {
                Step();
            }
        }
    }
}
