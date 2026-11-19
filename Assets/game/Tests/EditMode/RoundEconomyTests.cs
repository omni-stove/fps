using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Edit Mode では WeaponController が初期化されない (Awake が走らない) ので、持たせる武器は WeaponController.Definitions で見る。
    // ラウンドは SearchAndDestroy の Core を直に進め、Runner を通さずに RoundEconomy だけを試す。
    public sealed class RoundEconomyTests
    {
        private const float PreparationSeconds = 5f;
        private const string MissingWeaponId = "3a4b5c6d-0000-4000-8000-000000000001";
        private const string FragId = "frag";
        private const int LauncherPrice = 500;
        private const int HandgunPrice = 200;

        private readonly List<Object> _objects = new List<Object>();
        private WeaponDefinition _rifle;
        private WeaponDefinition _launcher;
        private WeaponDefinition _handgun;
        private SideMember _alphaOne;
        private SideMember _alphaTwo;
        private SideMember _bravoOne;
        private SideMember _bravoTwo;
        private SearchAndDestroy _game;
        private RoundEconomy _economy;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _rifle = Weapon("Rifle", TestWeapons.RifleSpec());
            _launcher = Weapon("Launcher", TestWeapons.LauncherSpec());
            _handgun = Weapon("Handgun", TestWeapons.HandgunSpec());

            _alphaOne = Member("A-1", 0, withController: true);
            _alphaTwo = Member("A-2", 0, withController: false);
            _bravoOne = Member("B-1", 1, withController: true);
            _bravoTwo = Member("B-2", 1, withController: false);
            Controller(_alphaOne).Definitions = new[] { _rifle, _handgun };
            Controller(_bravoOne).Definitions = new[] { _rifle };
        }

        [TearDown]
        public void TearDown()
        {
            _economy?.Dispose();
            _economy = null;
            GameSignals.ResetAll();
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                if (_objects[i] != null)
                {
                    Object.DestroyImmediate(_objects[i]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void TheBuyTimeIsOpenOnlyWhileTheRoundIsPreparing()
        {
            Start(swapEveryRounds: 0);

            Assert.That(_economy.Economy.IsBuyTime, Is.True);
            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)new WalletStatus(800, 16000, true)));
            Assert.That(GameSignals.BuySender.Value, Is.SameAs(_economy));

            _game.Tick(PreparationSeconds);

            Assert.That(_game.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(_economy.Economy.IsBuyTime, Is.False);
            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)new WalletStatus(800, 16000, false)));
            Assert.That(_economy.Buy(0, _launcher.Id), Is.EqualTo(PurchaseVerdict.NotBuyTime));
            Assert.That(Controller(_alphaOne).Definitions, Is.EqualTo(new[] { _rifle, _handgun }));
        }

        [Test]
        public void AWeaponBoughtInTheBuyTimeTakesItsSlotAndIsHeldWhenTheRoundStarts()
        {
            Start(swapEveryRounds: 0);

            GameSignals.BuySender.Value.Buy(_launcher.Id);

            Assert.That(_economy.Economy.MoneyOf(0), Is.EqualTo(800 - LauncherPrice));
            Assert.That(GameSignals.Wallet.Value.Value.Money, Is.EqualTo(800 - LauncherPrice));
            Assert.That(Controller(_alphaOne).Definitions, Is.EqualTo(new[] { _launcher, _handgun }), "slot 0 is replaced, slot 1 is kept");

            _game.Tick(PreparationSeconds);

            Assert.That(_game.Phase, Is.EqualTo(RoundPhase.Live));
            Assert.That(Controller(_alphaOne).Definitions, Is.EqualTo(new[] { _launcher, _handgun }));
        }

        [Test]
        public void TheLocalPlayersPurchaseIsAnsweredAfterTheWalletChanges()
        {
            Start(swapEveryRounds: 0);
            var answers = new List<PurchaseAnswer>();
            int? moneyWhenAnswered = null;
            void Record(PurchaseAnswer answer)
            {
                answers.Add(answer);
                moneyWhenAnswered = GameSignals.Wallet.Value?.Money;
            }

            CombatEvents.PurchaseAnswered += Record;
            try
            {
                GameSignals.BuySender.Value.Buy(_launcher.Id);
            }
            finally
            {
                CombatEvents.PurchaseAnswered -= Record;
            }

            Assert.That(answers, Has.Count.EqualTo(1));
            Assert.That(answers[0].ItemId, Is.EqualTo(_launcher.Id));
            Assert.That(answers[0].Verdict, Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(moneyWhenAnswered, Is.EqualTo(800 - LauncherPrice), "the wallet is published before the answer");
        }

        [Test]
        public void ALocalPurchaseThatCannotBeCarriedIsAnsweredWithTheReason()
        {
            Start(swapEveryRounds: 0);
            var answers = new List<PurchaseAnswer>();
            CombatEvents.PurchaseAnswered += answers.Add;
            try
            {
                GameSignals.BuySender.Value.Buy(_rifle.Id);
                GameSignals.BuySender.Value.Buy("unknown");
            }
            finally
            {
                CombatEvents.PurchaseAnswered -= answers.Add;
            }

            Assert.That(answers, Has.Count.EqualTo(2));
            Assert.That(answers[0].ItemId, Is.EqualTo(_rifle.Id));
            Assert.That(answers[0].Verdict, Is.EqualTo(PurchaseVerdict.CannotCarry), "the rifle is already the first weapon in slot 0");
            Assert.That(answers[1].ItemId, Is.EqualTo("unknown"));
            Assert.That(answers[1].Verdict, Is.EqualTo(PurchaseVerdict.UnknownItem));
            Assert.That(_economy.Economy.MoneyOf(0), Is.EqualTo(800));
        }

        [Test]
        public void PurchasesForOtherPlayersAreNotAnswered()
        {
            Start(swapEveryRounds: 0);
            var answers = new List<PurchaseAnswer>();
            CombatEvents.PurchaseAnswered += answers.Add;
            try
            {
                Assert.That(_economy.Buy(2, _handgun.Id), Is.EqualTo(PurchaseVerdict.Bought));
                Assert.That(_economy.Buy(0, _rifle.Id), Is.EqualTo(PurchaseVerdict.CannotCarry));
            }
            finally
            {
                CombatEvents.PurchaseAnswered -= answers.Add;
            }

            Assert.That(answers, Is.Empty, "only a purchase sent through the buy sender is answered");
        }

        [Test]
        public void ABoughtWeaponCanFillASlotTheHolderDidNotHave()
        {
            Start(swapEveryRounds: 0);

            Assert.That(_economy.Buy(2, _handgun.Id), Is.EqualTo(PurchaseVerdict.Bought));

            Assert.That(Controller(_bravoOne).Definitions, Is.EqualTo(new[] { _rifle, _handgun }));
        }

        [Test]
        public void KillsAndTheRoundResultPayAndADeathLosesTheBoughtWeapon()
        {
            Start(swapEveryRounds: 0);
            _economy.Buy(0, _launcher.Id);
            _game.Tick(PreparationSeconds);

            KillAlpha(_bravoOne, _alphaOne);
            _economy.Killed(_alphaTwo, _alphaOne);
            KillAlpha(_bravoTwo, _alphaTwo);
            _game.Tick(0f);

            Assert.That(_game.LastRoundWinner, Is.EqualTo((Side?)Side.Bravo));
            Assert.That(_game.Phase, Is.EqualTo(RoundPhase.Preparing));
            Assert.That(_economy.Economy.MoneyOf(0), Is.EqualTo(800 - LauncherPrice + 1400));
            Assert.That(_economy.Economy.MoneyOf(1), Is.EqualTo(800 + 1400), "a team kill pays nothing");
            Assert.That(_economy.Economy.MoneyOf(2), Is.EqualTo(800 + 300 + 3000));
            Assert.That(_economy.Economy.MoneyOf(3), Is.EqualTo(800 + 300 + 3000));
            Assert.That(_economy.Economy.WeaponIn(0, 0), Is.Null);
            Assert.That(Controller(_alphaOne).Definitions, Is.EqualTo(new[] { _rifle, _handgun }), "the next round starts with the first weapons again");
            Assert.That(_economy.Economy.IsBuyTime, Is.True);
        }

        [Test]
        public void ASurvivorKeepsTheBoughtWeaponAndTheMoneyIntoTheNextRound()
        {
            Start(swapEveryRounds: 0);
            _economy.Buy(2, _handgun.Id);
            _game.Tick(PreparationSeconds);

            KillAlpha(_bravoOne, _alphaOne);
            KillAlpha(_bravoOne, _alphaTwo);
            _game.Tick(0f);

            Assert.That(_economy.Economy.MoneyOf(2), Is.EqualTo(800 - HandgunPrice + 300 + 300 + 3000));
            Assert.That(Controller(_bravoOne).Definitions, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(_economy.Buy(2, _handgun.Id), Is.EqualTo(PurchaseVerdict.CannotCarry), "the handgun is still held");
        }

        [Test]
        public void SwappingSidesStartsTheWalletsAgain()
        {
            Start(swapEveryRounds: 1);
            _economy.Buy(2, _handgun.Id);
            _game.Tick(PreparationSeconds);

            KillAlpha(_bravoOne, _alphaOne);
            KillAlpha(_bravoOne, _alphaTwo);
            _game.Tick(0f);

            Assert.That(_game.Attacker, Is.EqualTo(Side.Bravo));
            Assert.That(_economy.Economy.MoneyOf(2), Is.EqualTo(800));
            Assert.That(_economy.Economy.MoneyOf(0), Is.EqualTo(800));
            Assert.That(Controller(_bravoOne).Definitions, Is.EqualTo(new[] { _rifle }), "the bought weapons go with the reset");
            Assert.That(GameSignals.Wallet.Value, Is.EqualTo((WalletStatus?)new WalletStatus(800, 16000, true)));
        }

        [Test]
        public void WhatTheHolderCanNotCarryIsRefusedWithoutTakingMoney()
        {
            Start(swapEveryRounds: 0);
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape(MissingWeaponId)));

            Assert.That(_economy.Buy(0, MissingWeaponId), Is.EqualTo(PurchaseVerdict.CannotCarry), "the weapon is not in the catalog");
            Assert.That(_economy.Buy(0, FragId), Is.EqualTo(PurchaseVerdict.CannotCarry), "there is no GrenadeThrower");
            Assert.That(_economy.Buy(1, _launcher.Id), Is.EqualTo(PurchaseVerdict.CannotCarry), "there is no WeaponController");
            Assert.That(_economy.Buy(0, "unknown"), Is.EqualTo(PurchaseVerdict.UnknownItem));
            Assert.That(_economy.Buy(0, _rifle.Id), Is.EqualTo(PurchaseVerdict.CannotCarry), "the rifle is already the first weapon in slot 0");
            Assert.That(_economy.Economy.MoneyOf(0), Is.EqualTo(800));
            Assert.That(_economy.Economy.MoneyOf(1), Is.EqualTo(800));
        }

        [Test]
        public void TheFirstWeaponInTheSameSlotIsRefusedUntilAnotherIsBoughtThere()
        {
            Start(swapEveryRounds: 0);

            Assert.That(_economy.Buy(2, _rifle.Id), Is.EqualTo(PurchaseVerdict.CannotCarry), "the rifle is already the first weapon in slot 0");
            Assert.That(_economy.Economy.MoneyOf(2), Is.EqualTo(800));
            Assert.That(_economy.Economy.WeaponIn(2, 0), Is.Null);
            Assert.That(Controller(_bravoOne).Definitions, Is.EqualTo(new[] { _rifle }));

            Assert.That(_economy.Buy(2, _launcher.Id), Is.EqualTo(PurchaseVerdict.Bought));
            Assert.That(_economy.Buy(2, _rifle.Id), Is.EqualTo(PurchaseVerdict.NotEnoughMoney), "the launcher now holds slot 0");
            Assert.That(_economy.Economy.MoneyOf(2), Is.EqualTo(800 - LauncherPrice));
            Assert.That(Controller(_bravoOne).Definitions, Is.EqualTo(new[] { _launcher }));
        }

        [Test]
        public void WithoutALocalPlayerNothingIsPublished()
        {
            Start(swapEveryRounds: 0, localPlayerId: null);

            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(GameSignals.BuySender.Value, Is.Null);
            Assert.That(_economy.Buy(2, _handgun.Id), Is.EqualTo(PurchaseVerdict.Bought), "bots can still be made to buy");
        }

        [Test]
        public void DisposeClearsTheSignalsAndStopsListening()
        {
            Start(swapEveryRounds: 0);
            _game.Tick(PreparationSeconds);

            _economy.Dispose();
            KillAlpha(_bravoOne, _alphaOne);
            KillAlpha(_bravoOne, _alphaTwo);
            _game.Tick(0f);

            Assert.That(GameSignals.Wallet.HasValue, Is.False);
            Assert.That(GameSignals.BuySender.Value, Is.Null);
            Assert.That(_economy.Economy.MoneyOf(2), Is.EqualTo(800), "a disposed economy pays nothing");
            Assert.Throws<ObjectDisposedException>(() => _economy.Buy(0, _handgun.Id));
        }

        [Test]
        public void TheConstructorRejectsALocalPlayerOutsideTheMembers()
        {
            var game = new SearchAndDestroy(new SearchAndDestroySpec(siteCount: 1));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new RoundEconomy(Rules(), game.Rounds, new[] { _alphaOne }, 3, FindWeapon));
            Assert.Throws<ArgumentNullException>(() => new RoundEconomy(null, game.Rounds, new[] { _alphaOne }, 0, FindWeapon));
            Assert.Throws<ArgumentNullException>(() => new RoundEconomy(Rules(), game.Rounds, new[] { _alphaOne }, 0, null));
        }

        private void Start(int swapEveryRounds, int? localPlayerId = 0)
        {
            _game = new SearchAndDestroy(
                new SearchAndDestroySpec(siteCount: 1, winsToWin: 5, preparationSeconds: PreparationSeconds, swapEveryRounds: swapEveryRounds));
            var members = new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo };
            for (var id = 0; id < members.Length; id++)
            {
                _game.AddPlayer(id, members[id].Side);
            }

            _economy = new RoundEconomy(Rules(), _game.Rounds, members, localPlayerId, FindWeapon);
            _game.Start();
        }

        private void KillAlpha(SideMember killer, SideMember victim)
        {
            var killerId = IdOf(killer);
            var victimId = IdOf(victim);
            _game.ReportKill(killerId, victimId);
            _economy.Killed(killer, victim);
        }

        private int IdOf(SideMember member)
        {
            return Array.IndexOf(new[] { _alphaOne, _alphaTwo, _bravoOne, _bravoTwo }, member);
        }

        private EconomyRules Rules()
        {
            return new EconomyRules(
                EconomySpec.Default,
                new BuyCatalog(new[]
                {
                    new BuyItem(_rifle.Id, BuyItemKind.Weapon, 2700, slot: 0),
                    new BuyItem(_launcher.Id, BuyItemKind.Weapon, LauncherPrice, slot: 0),
                    new BuyItem(_handgun.Id, BuyItemKind.Weapon, HandgunPrice, slot: 1),
                    new BuyItem(MissingWeaponId, BuyItemKind.Weapon, 100, slot: 0),
                    new BuyItem(FragId, BuyItemKind.Throwable, 300),
                }));
        }

        private WeaponDefinition FindWeapon(string weaponId, out string failure)
        {
            failure = null;
            foreach (var weapon in new[] { _rifle, _launcher, _handgun })
            {
                if (weapon.Id == weaponId)
                {
                    return weapon;
                }
            }

            failure = "not in the test catalog";
            return null;
        }

        private static WeaponController Controller(SideMember member)
        {
            return member.GetComponentInChildren<WeaponController>(true);
        }

        private SideMember Member(string name, int side, bool withController)
        {
            var go = Track(new GameObject(name));
            go.AddComponent<Damageable>().Initialize(name, Damageable.DefaultMaxHealth);
            var member = go.AddComponent<SideMember>();
            member.Side = new Side(side);
            if (withController)
            {
                var view = new GameObject(name + "-Camera");
                view.transform.SetParent(go.transform, false);
                view.AddComponent<WeaponController>();
            }

            return member;
        }

        private WeaponDefinition Weapon(string name, WeaponSpec spec)
        {
            var definition = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            definition.name = name;
            WeaponAssets.Configure(definition, spec);
            return definition;
        }

        private T Track<T>(T obj)
            where T : Object
        {
            _objects.Add(obj);
            return obj;
        }
    }
}
