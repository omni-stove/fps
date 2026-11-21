using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerAbilityChoicesTests
    {
        private const string _turret = "deploy-turret";
        private const string _shield = "deploy-shield";
        private const string _decoy = "deploy-decoy";
        private const string _beacon = "deploy-beacon";
        private const string _drone = "deploy-drone";
        private const string _grapple = "grapple";
        private const string _none = AbilitySelection.None;

        [Test]
        public void WithoutListedAbilitiesEveryTypeIsACandidateAndTheFirstTwoAreTheDefault()
        {
            var choices = Choices();

            Use(choices, null);

            Assert.That(choices.SlotCount, Is.EqualTo(AbilitySelection.DefaultSlotCount));
            Assert.That(choices.Candidates, Is.EqualTo(Ids(_turret, _shield, _decoy, _beacon)));
            Assert.That(choices.Default, Is.EqualTo(Ids(_turret, _shield)));
            Assert.That(choices.AbilitiesFor(1), Is.EqualTo(Ids(_turret, _shield)));
            Assert.That(choices.HasChoice(1), Is.False);
        }

        [Test]
        public void ListedAbilitiesAreTheOnlyCandidatesAndAShortListLeavesTheOtherSlotEmpty()
        {
            var choices = Choices();

            Use(choices, Ids(_beacon));

            Assert.That(choices.Candidates, Is.EqualTo(Ids(_beacon)));
            Assert.That(choices.Default, Is.EqualTo(Ids(_beacon, _none)));
            Assert.That(choices.IsEquipped(1, _beacon), Is.True);
            Assert.That(choices.IsEquipped(1, _shield), Is.False);
            Assert.That(choices.IsEquipped(1, _none), Is.False, "an empty slot equips nothing");
        }

        [Test]
        public void AnEmptyListOffersNothing()
        {
            var choices = Choices();

            Use(choices, Ids());

            Assert.That(choices.Candidates, Is.Empty);
            Assert.That(choices.Default, Is.EqualTo(Ids(_none, _none)));
            Assert.That(choices.IsEquipped(1, _turret), Is.False);
        }

        [Test]
        public void AListedIdThatIsNotInTheCatalogIsSkippedAndUnusableTypesAreLeftOut()
        {
            var choices = new ServerAbilityChoices(new DeployCatalog(new[] { Type(_drone, DeployRole.Drone), Type(_shield, DeployRole.Shield) }));
            var skipped = new List<string>();

            choices.Use(Ids("deploy-missing", _drone, _none, _shield), id => id != _drone, skipped, null);

            Assert.That(skipped, Is.EqualTo(Ids("deploy-missing")));
            Assert.That(choices.Candidates, Is.EqualTo(Ids(_shield)));
            Assert.That(choices.Default, Is.EqualTo(Ids(_shield, _none)));
        }

        [Test]
        public void ARequestIsCheckedAgainstTheCandidatesTheSlotsAndRepeats()
        {
            var choices = Choices();
            Use(choices, Ids(_shield, _decoy));

            Assert.That(choices.Check(Ids(_decoy, _none)), Is.Null);
            Assert.That(choices.Check(Ids(_none, _none)), Is.Null, "every slot may be empty");
            Assert.That(choices.Check(Ids(_decoy, _shield)), Is.Null);
            Assert.That(choices.Check(Ids(_decoy)), Is.EqualTo("the request has 1 ability slots but the loadout has 2"));
            Assert.That(choices.Check(Ids(_decoy, _shield, _none)), Is.EqualTo("the request has 3 ability slots but the loadout has 2"));
            Assert.That(
                choices.Check(Ids(_none, _turret)),
                Is.EqualTo("ability slot 1 names an ability the rules do not offer"),
                "the turret is in the catalog but not listed");
            Assert.That(
                choices.Check(Ids("made-up\nline", _none)),
                Is.EqualTo("ability slot 0 names an ability the rules do not offer"),
                "the sent id is not written into the reason");
            Assert.That(choices.Check(Ids(_shield, _shield)), Is.EqualTo("ability slot 1 names the same ability as slot 0"));
            Assert.Throws<ArgumentNullException>(() => choices.Check(null));
        }

        [Test]
        public void AChoiceIsEquippedFromTheNextSpawn()
        {
            var choices = Choices();
            Use(choices, null);
            Assert.That(choices.Equip(1), Is.True, "the first spawn equips the default");
            Assert.That(choices.EquippedFor(1), Is.EqualTo(Ids(_turret, _shield)));

            choices.Choose(1, Ids(_beacon, _none));

            Assert.That(choices.HasChoice(1), Is.True);
            Assert.That(choices.AbilitiesFor(1), Is.EqualTo(Ids(_beacon, _none)));
            Assert.That(choices.IsEquipped(1, _turret), Is.True, "the abilities held now do not change before the next spawn");
            Assert.That(choices.IsEquipped(1, _beacon), Is.False);

            Assert.That(choices.Equip(1), Is.True);
            Assert.That(choices.IsEquipped(1, _beacon), Is.True);
            Assert.That(choices.IsEquipped(1, _turret), Is.False);
            Assert.That(choices.Equip(1), Is.False, "the same abilities again are not a change");
        }

        [Test]
        public void ChoosingAnInvalidListThrowsAndKeepsNoChoice()
        {
            var choices = Choices();
            Use(choices, Ids(_shield));

            var error = Assert.Throws<ArgumentException>(() => choices.Choose(1, Ids(_decoy, _none)));

            Assert.That(error.Message, Does.StartWith("ability slot 0 names an ability the rules do not offer"));
            Assert.That(choices.HasChoice(1), Is.False);
        }

        [Test]
        public void ForgettingWhatWasEquippedGivesTheChoiceAtTheNextLookUp()
        {
            var choices = Choices();
            Use(choices, null);
            choices.Equip(1);
            choices.Choose(1, Ids(_decoy, _beacon));

            choices.ForgetEquipped();

            Assert.That(choices.IsEquipped(1, _decoy), Is.True);
            Assert.That(choices.EquippedFor(1), Is.EqualTo(Ids(_decoy, _beacon)));
            Assert.That(choices.Equip(1), Is.False, "the look-up already equipped the choice");
        }

        [Test]
        public void NewCandidatesDropChoicesTheyNoLongerOfferAndKeepTheOthers()
        {
            var choices = Choices();
            Use(choices, null);
            choices.Choose(1, Ids(_turret, _none));
            choices.Choose(2, Ids(_beacon, _none));
            choices.Choose(3, Ids(_none, _none));
            choices.Equip(2);
            var dropped = new List<int>();

            choices.Use(Ids(_beacon, _shield), null, null, dropped);

            Assert.That(dropped, Is.EqualTo(new List<int> { 1 }));
            Assert.That(choices.HasChoice(1), Is.False);
            Assert.That(choices.AbilitiesFor(1), Is.EqualTo(Ids(_beacon, _shield)), "a dropped choice falls back to the new default");
            Assert.That(choices.AbilitiesFor(2), Is.EqualTo(Ids(_beacon, _none)));
            Assert.That(choices.AbilitiesFor(3), Is.EqualTo(Ids(_none, _none)));
            Assert.That(choices.Equip(2), Is.True, "what was equipped under the old rules is thrown away");
        }

        [Test]
        public void RemovingAPlayerForgetsTheirChoiceAndWhatTheyHeld()
        {
            var choices = Choices();
            Use(choices, null);
            choices.Choose(1, Ids(_decoy, _none));
            choices.Equip(1);

            Assert.That(choices.Remove(1), Is.True);
            Assert.That(choices.Remove(1), Is.False);
            Assert.That(choices.HasChoice(1), Is.False);
            Assert.That(choices.AbilitiesFor(1), Is.EqualTo(Ids(_turret, _shield)));
            Assert.That(choices.Equip(1), Is.True);
        }

        [Test]
        public void WithoutACatalogOnlyEmptySlotsAreAccepted()
        {
            var choices = new ServerAbilityChoices();

            Assert.That(choices.Candidates, Is.Empty);
            Assert.That(choices.Default, Is.EqualTo(Ids(_none, _none)));
            Assert.That(choices.Check(Ids(_none, _none)), Is.Null);
            Assert.That(choices.Check(Ids(_shield, _none)), Is.EqualTo("ability slot 0 names an ability the rules do not offer"));
            Assert.That(choices.IsEquipped(1, _shield), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => _ = new ServerAbilityChoices(null, -1));
        }

        [Test]
        public void WithoutListedAbilitiesTheGrappleIsOfferedAfterTheDeployables()
        {
            var choices = Choices(Grapple());

            Use(choices, null);

            Assert.That(choices.Grapple.Id, Is.EqualTo(_grapple));
            Assert.That(choices.Candidates, Is.EqualTo(Ids(_turret, _shield, _decoy, _beacon, _grapple)));
            Assert.That(choices.Default, Is.EqualTo(Ids(_turret, _shield)));
            Assert.That(choices.HasGrapple(1), Is.False, "the default slots hold two deployables");
        }

        [Test]
        public void TheGrappleCanBeListedAndChosen()
        {
            var choices = Choices(Grapple());
            var skipped = new List<string>();

            choices.Use(Ids(_grapple, _shield, "hook-missing"), null, skipped, null);

            Assert.That(skipped, Is.EqualTo(Ids("hook-missing")));
            Assert.That(choices.Candidates, Is.EqualTo(Ids(_grapple, _shield)));
            Assert.That(choices.IsGrapple(_grapple), Is.True);
            Assert.That(choices.IsGrapple(_shield), Is.False);
            Assert.That(choices.IsGrapple(null), Is.False);
            Assert.That(choices.HasGrapple(1), Is.True, "the grapple is first in the default");

            choices.Choose(2, Ids(_none, _shield));
            choices.Equip(2);
            Assert.That(choices.HasGrapple(2), Is.False);

            choices.Choose(2, Ids(_shield, _grapple));
            Assert.That(choices.HasGrapple(2), Is.False, "the choice is held from the next spawn");
            choices.Equip(2);
            Assert.That(choices.HasGrapple(2), Is.True);
        }

        [Test]
        public void WithoutAGrappleItsIdIsNotOffered()
        {
            var choices = Choices();
            var skipped = new List<string>();

            choices.Use(Ids(_grapple, _shield), null, skipped, null);

            Assert.That(choices.Grapple, Is.Null);
            Assert.That(skipped, Is.EqualTo(Ids(_grapple)));
            Assert.That(choices.Candidates, Is.EqualTo(Ids(_shield)));
            Assert.That(choices.IsGrapple(_grapple), Is.False);
            Assert.That(choices.HasGrapple(1), Is.False);
        }

        [Test]
        public void AnUnusableGrappleIsLeftOutLikeAnUnusableDeployable()
        {
            var choices = Choices(Grapple());

            choices.Use(null, id => id != _grapple, null, null);

            Assert.That(choices.Candidates, Does.Not.Contain(_grapple));
        }

        [Test]
        public void AGrappleThatSharesADeployableIdIsRejected()
        {
            var e = Assert.Throws<ArgumentException>(
                () => _ = Choices(new GrappleType(_shield, GrappleSpec.Default, AbilitySpec.Default)));

            Assert.That(e.ParamName, Is.EqualTo("grapple"));
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Ids(params string[] ids) => ids;

        private static GrappleType Grapple()
        {
            return new GrappleType(_grapple, GrappleSpec.Default, AbilitySpec.Default);
        }

        private static ServerAbilityChoices Choices(GrappleType grapple = null)
        {
            return new ServerAbilityChoices(
                new DeployCatalog(new[]
                {
                    Type(_turret, DeployRole.Turret),
                    Type(_shield, DeployRole.Shield),
                    Type(_decoy, DeployRole.Decoy),
                    Type(_beacon, DeployRole.Beacon),
                }),
                AbilitySelection.DefaultSlotCount,
                grapple);
        }

        private static void Use(ServerAbilityChoices choices, IReadOnlyList<string> listed)
        {
            choices.Use(listed, null, null, null);
        }

        private static DeployType Type(string id, DeployRole role)
        {
            switch (role)
            {
                case DeployRole.Turret:
                    return new DeployType(id, role, DeployType.DefaultDeploy(role), TurretSpec.Default, TestRules.RifleId);
                case DeployRole.Decoy:
                    return new DeployType(id, role, DeployType.DefaultDeploy(role), decoy: DecoySpec.Default);
                case DeployRole.Beacon:
                    return new DeployType(id, role, DeployType.DefaultDeploy(role), beacon: BeaconSpec.Default);
                case DeployRole.Drone:
                    return new DeployType(id, role, DeployType.DefaultDeploy(role), drone: DroneSpec.Default);
                default:
                    return new DeployType(id, role, DeployType.DefaultDeploy(role));
            }
        }
    }
}
