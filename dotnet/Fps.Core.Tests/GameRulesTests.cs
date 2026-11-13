using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GameRulesTests
    {
        private static GameRules Rules(
            FriendlyFire friendlyFire = FriendlyFire.Off,
            RespawnMode respawnMode = RespawnMode.Individual,
            float waveIntervalSeconds = 10f,
            float regenDelaySeconds = 0f,
            float regenPerSecond = 0f,
            DeathBody deathBody = DeathBody.Vanish,
            DeathCamera deathCamera = DeathCamera.None,
            bool infiniteReserve = false,
            float reserveAmmoMultiplier = 1f,
            Destruction destruction = Destruction.Off,
            float spawnProtectionSeconds = 0f,
            FallDamage fallDamage = FallDamage.Off,
            HudDensity hudDensity = HudDensity.Full)
        {
            return new GameRules(
                friendlyFire,
                respawnMode,
                waveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                deathBody,
                deathCamera,
                infiniteReserve,
                reserveAmmoMultiplier,
                destruction,
                null,
                spawnProtectionSeconds,
                fallDamage,
                hudDensity);
        }

        [Test]
        public void DefaultMatchesTheCurrentBehaviour()
        {
            var rules = GameRules.Default;

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(GameRules.DefaultWaveIntervalSeconds));
            Assert.That(rules.WaveIntervalSeconds, Is.GreaterThan(0f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(0f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(0f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Vanish));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.None));
            Assert.That(rules.InfiniteReserve, Is.False);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
            Assert.That(rules.Destruction, Is.EqualTo(Destruction.Off));
            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(0f));
            Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.Off));
            Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Full));
        }

        [Test]
        public void NewValuesAreOffWhenOmitted()
        {
            var rules = new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                10f,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off);

            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(0f));
            Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.Off));
            Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Full));
        }

        [Test]
        public void KeepsTheGivenSpawnProtectionFallDamageAndHudDensity()
        {
            var rules = Rules(spawnProtectionSeconds: 2.5f, fallDamage: FallDamage.On, hudDensity: HudDensity.Hardcore);

            Assert.That(rules.SpawnProtectionSeconds, Is.EqualTo(2.5f));
            Assert.That(rules.FallDamage, Is.EqualTo(FallDamage.On));
            Assert.That(rules.HudDensity, Is.EqualTo(HudDensity.Hardcore));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteSpawnProtection(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(spawnProtectionSeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("spawnProtectionSeconds"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void RejectsAnUndefinedFallDamage(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(fallDamage: (FallDamage)value));
            Assert.That(ex.ParamName, Is.EqualTo("fallDamage"));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void RejectsAnUndefinedHudDensity(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(hudDensity: (HudDensity)value));
            Assert.That(ex.ParamName, Is.EqualTo("hudDensity"));
        }

        [Test]
        public void DeathCameraHasNoneKillerAllyAndFreeInThatOrder()
        {
            // 値の並びがアセットとネットの直列化に乗るので、後ろに足したことを固定する。
            Assert.That((int)DeathCamera.None, Is.EqualTo(0));
            Assert.That((int)DeathCamera.Killer, Is.EqualTo(1));
            Assert.That((int)DeathCamera.Ally, Is.EqualTo(2));
            Assert.That((int)DeathCamera.Free, Is.EqualTo(3));
        }

        [Test]
        public void DefaultIsTheSameInstanceEveryTime()
        {
            var first = GameRules.Default;
            var second = GameRules.Default;

            Assert.That(first, Is.SameAs(second));
        }

        [Test]
        public void KeepsEveryGivenValue()
        {
            var rules = new GameRules(
                FriendlyFire.Reflect,
                RespawnMode.Wave,
                15f,
                4f,
                12.5f,
                DeathBody.Collapse,
                DeathCamera.Killer,
                true,
                2.5f,
                Destruction.On);

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Reflect));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(15f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(4f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(12.5f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Killer));
            Assert.That(rules.InfiniteReserve, Is.True);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(2.5f));
            Assert.That(rules.Destruction, Is.EqualTo(Destruction.On));
        }

        [Test]
        public void AcceptsEveryDefinedEnumValue()
        {
            foreach (FriendlyFire value in Enum.GetValues<FriendlyFire>())
            {
                Assert.That(Rules(friendlyFire: value).FriendlyFire, Is.EqualTo(value));
            }

            foreach (RespawnMode value in Enum.GetValues<RespawnMode>())
            {
                Assert.That(Rules(respawnMode: value).RespawnMode, Is.EqualTo(value));
            }

            foreach (DeathBody value in Enum.GetValues<DeathBody>())
            {
                Assert.That(Rules(deathBody: value).DeathBody, Is.EqualTo(value));
            }

            foreach (DeathCamera value in Enum.GetValues<DeathCamera>())
            {
                Assert.That(Rules(deathCamera: value).DeathCamera, Is.EqualTo(value));
            }

            foreach (Destruction value in Enum.GetValues<Destruction>())
            {
                Assert.That(Rules(destruction: value).Destruction, Is.EqualTo(value));
            }

            foreach (FallDamage value in Enum.GetValues<FallDamage>())
            {
                Assert.That(Rules(fallDamage: value).FallDamage, Is.EqualTo(value));
            }

            foreach (HudDensity value in Enum.GetValues<HudDensity>())
            {
                Assert.That(Rules(hudDensity: value).HudDensity, Is.EqualTo(value));
            }
        }

        [Test]
        public void AcceptsZeroForTheNonNegativeValues()
        {
            var rules = Rules(regenDelaySeconds: 0f, regenPerSecond: 0f, reserveAmmoMultiplier: 0f);

            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(0f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(0f));
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(0f));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void RejectsAnUndefinedFriendlyFire(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(friendlyFire: (FriendlyFire)value));
            Assert.That(ex.ParamName, Is.EqualTo("friendlyFire"));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void RejectsAnUndefinedRespawnMode(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(respawnMode: (RespawnMode)value));
            Assert.That(ex.ParamName, Is.EqualTo("respawnMode"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void RejectsAnUndefinedDeathBody(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(deathBody: (DeathBody)value));
            Assert.That(ex.ParamName, Is.EqualTo("deathBody"));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void RejectsAnUndefinedDeathCamera(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(deathCamera: (DeathCamera)value));
            Assert.That(ex.ParamName, Is.EqualTo("deathCamera"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void RejectsAnUndefinedDestruction(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(destruction: (Destruction)value));
            Assert.That(ex.ParamName, Is.EqualTo("destruction"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANonPositiveOrNonFiniteWaveInterval(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(waveIntervalSeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("waveIntervalSeconds"));
        }

        [Test]
        public void RejectsAnInvalidWaveIntervalEvenWhenNotInWaveMode()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => Rules(respawnMode: RespawnMode.Individual, waveIntervalSeconds: 0f));
            Assert.That(ex.ParamName, Is.EqualTo("waveIntervalSeconds"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteRegenDelay(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(regenDelaySeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("regenDelaySeconds"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteRegenRate(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(regenPerSecond: value));
            Assert.That(ex.ParamName, Is.EqualTo("regenPerSecond"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteReserveAmmoMultiplier(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(reserveAmmoMultiplier: value));
            Assert.That(ex.ParamName, Is.EqualTo("reserveAmmoMultiplier"));
        }

        private static ActionRules Actions(
            Downed downed = Downed.Off,
            float downedBleedoutSeconds = 15f,
            float downedHealthFraction = 0.3f,
            float reviveSeconds = 3f,
            Cover cover = Cover.Off,
            BlindFire blindFire = BlindFire.Off,
            ActiveReload activeReload = ActiveReload.Off,
            Dodge dodge = Dodge.Off,
            Vault vault = Vault.Off,
            Grenade grenade = Grenade.Off,
            int grenadeCount = 0)
        {
            return new ActionRules(
                downed,
                downedBleedoutSeconds,
                downedHealthFraction,
                reviveSeconds,
                cover,
                blindFire,
                activeReload,
                dodge,
                vault,
                grenade,
                grenadeCount);
        }

        [Test]
        public void ActionsAreTheDefaultWhenOmitted()
        {
            Assert.That(Rules().Actions, Is.SameAs(ActionRules.Default));
        }

        [Test]
        public void ActionsAreTheDefaultWhenNullIsGiven()
        {
            var rules = new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                10f,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off,
                null);

            Assert.That(rules.Actions, Is.SameAs(ActionRules.Default));
        }

        [Test]
        public void DefaultRulesUseTheDefaultActions()
        {
            Assert.That(GameRules.Default.Actions, Is.SameAs(ActionRules.Default));
        }

        [Test]
        public void KeepsTheGivenActions()
        {
            var actions = Actions(downed: Downed.Revive, cover: Cover.On);

            var rules = new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                10f,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off,
                actions);

            Assert.That(rules.Actions, Is.SameAs(actions));
        }

        [Test]
        public void DefaultActionsAreAllOff()
        {
            var actions = ActionRules.Default;

            Assert.That(actions.Downed, Is.EqualTo(Downed.Off));
            Assert.That(actions.DownedBleedoutSeconds, Is.EqualTo(15f));
            Assert.That(actions.DownedHealthFraction, Is.EqualTo(0.3f));
            Assert.That(actions.ReviveSeconds, Is.EqualTo(3f));
            Assert.That(actions.Cover, Is.EqualTo(Cover.Off));
            Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.Off));
            Assert.That(actions.ActiveReload, Is.EqualTo(ActiveReload.Off));
            Assert.That(actions.Dodge, Is.EqualTo(Dodge.Off));
            Assert.That(actions.Vault, Is.EqualTo(Vault.Off));
            Assert.That(actions.Grenade, Is.EqualTo(Grenade.Off));
            Assert.That(actions.GrenadeCount, Is.EqualTo(0));
        }

        [Test]
        public void ActionsLeaveDodgeVaultAndGrenadeOffWhenOmitted()
        {
            var actions = new ActionRules(Downed.Off, 15f, 0.3f, 3f, Cover.Off, BlindFire.Off, ActiveReload.Off);

            Assert.That(actions.Dodge, Is.EqualTo(Dodge.Off));
            Assert.That(actions.Vault, Is.EqualTo(Vault.Off));
            Assert.That(actions.Grenade, Is.EqualTo(Grenade.Off));
            Assert.That(actions.GrenadeCount, Is.EqualTo(0));
        }

        [Test]
        public void ActionsKeepTheGivenDodgeVaultAndGrenade()
        {
            var actions = Actions(dodge: Dodge.On, vault: Vault.On, grenade: Grenade.On, grenadeCount: 2);

            Assert.That(actions.Dodge, Is.EqualTo(Dodge.On));
            Assert.That(actions.Vault, Is.EqualTo(Vault.On));
            Assert.That(actions.Grenade, Is.EqualTo(Grenade.On));
            Assert.That(actions.GrenadeCount, Is.EqualTo(2));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ActionsRejectAnUndefinedDodge(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(dodge: (Dodge)value));
            Assert.That(ex.ParamName, Is.EqualTo("dodge"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ActionsRejectAnUndefinedVault(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(vault: (Vault)value));
            Assert.That(ex.ParamName, Is.EqualTo("vault"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ActionsRejectAnUndefinedGrenade(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(grenade: (Grenade)value));
            Assert.That(ex.ParamName, Is.EqualTo("grenade"));
        }

        [TestCase(Grenade.Off)]
        [TestCase(Grenade.On)]
        public void ActionsRejectANegativeGrenadeCount(Grenade grenade)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(grenade: grenade, grenadeCount: -1));
            Assert.That(ex.ParamName, Is.EqualTo("grenadeCount"));
        }

        [Test]
        public void DefaultActionsAreTheSameInstanceEveryTime()
        {
            var first = ActionRules.Default;

            Assert.That(ActionRules.Default, Is.SameAs(first));
        }

        [Test]
        public void ActionsKeepEveryGivenValue()
        {
            var actions = new ActionRules(Downed.Revive, 20f, 0.5f, 4.5f, Cover.On, BlindFire.On, ActiveReload.On);

            Assert.That(actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(actions.DownedBleedoutSeconds, Is.EqualTo(20f));
            Assert.That(actions.DownedHealthFraction, Is.EqualTo(0.5f));
            Assert.That(actions.ReviveSeconds, Is.EqualTo(4.5f));
            Assert.That(actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.That(actions.ActiveReload, Is.EqualTo(ActiveReload.On));
        }

        [Test]
        public void ActionsAcceptEveryDefinedEnumValue()
        {
            foreach (Downed value in Enum.GetValues<Downed>())
            {
                Assert.That(Actions(downed: value).Downed, Is.EqualTo(value));
            }

            foreach (Cover value in Enum.GetValues<Cover>())
            {
                Assert.That(Actions(cover: value).Cover, Is.EqualTo(value));
            }

            foreach (BlindFire value in Enum.GetValues<BlindFire>())
            {
                Assert.That(Actions(cover: Cover.On, blindFire: value).BlindFire, Is.EqualTo(value));
            }

            foreach (ActiveReload value in Enum.GetValues<ActiveReload>())
            {
                Assert.That(Actions(activeReload: value).ActiveReload, Is.EqualTo(value));
            }

            foreach (Dodge value in Enum.GetValues<Dodge>())
            {
                Assert.That(Actions(dodge: value).Dodge, Is.EqualTo(value));
            }

            foreach (Vault value in Enum.GetValues<Vault>())
            {
                Assert.That(Actions(vault: value).Vault, Is.EqualTo(value));
            }

            foreach (Grenade value in Enum.GetValues<Grenade>())
            {
                Assert.That(Actions(grenade: value).Grenade, Is.EqualTo(value));
            }
        }

        [Test]
        public void ActionsAcceptAFullHealthFraction()
        {
            Assert.That(Actions(downedHealthFraction: 1f).DownedHealthFraction, Is.EqualTo(1f));
        }

        [Test]
        public void ActionsAllowCoverWithoutBlindFire()
        {
            var actions = Actions(cover: Cover.On, blindFire: BlindFire.Off);

            Assert.That(actions.Cover, Is.EqualTo(Cover.On));
            Assert.That(actions.BlindFire, Is.EqualTo(BlindFire.Off));
        }

        [Test]
        public void ActionsRejectBlindFireWithoutCover()
        {
            var ex = Assert.Throws<ArgumentException>(() => Actions(cover: Cover.Off, blindFire: BlindFire.On));
            Assert.That(ex.ParamName, Is.EqualTo("blindFire"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ActionsRejectAnUndefinedDowned(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(downed: (Downed)value));
            Assert.That(ex.ParamName, Is.EqualTo("downed"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ActionsRejectAnUndefinedCover(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(cover: (Cover)value));
            Assert.That(ex.ParamName, Is.EqualTo("cover"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ActionsRejectAnUndefinedBlindFire(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(cover: Cover.On, blindFire: (BlindFire)value));
            Assert.That(ex.ParamName, Is.EqualTo("blindFire"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void ActionsRejectAnUndefinedActiveReload(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(activeReload: (ActiveReload)value));
            Assert.That(ex.ParamName, Is.EqualTo("activeReload"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ActionsRejectANonPositiveOrNonFiniteBleedout(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(downedBleedoutSeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("downedBleedoutSeconds"));
        }

        [TestCase(0f)]
        [TestCase(-0.1f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ActionsRejectAHealthFractionOutsideZeroToOne(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(downedHealthFraction: value));
            Assert.That(ex.ParamName, Is.EqualTo("downedHealthFraction"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ActionsRejectANonPositiveOrNonFiniteReviveTime(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(reviveSeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("reviveSeconds"));
        }

        [Test]
        public void ActionsRejectAnInvalidDownedValueEvenWhenDownedIsOff()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Actions(downed: Downed.Off, reviveSeconds: 0f));
            Assert.That(ex.ParamName, Is.EqualTo("reviveSeconds"));
        }

        [Test]
        public void TeamDeathmatchSpecUsesTheDefaultRulesWhenNoneAreGiven()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f);

            Assert.That(spec.Rules, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void TeamDeathmatchSpecUsesTheDefaultRulesWhenNullIsGiven()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f, null);

            Assert.That(spec.Rules, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void TeamDeathmatchSpecKeepsTheGivenRules()
        {
            var rules = Rules(friendlyFire: FriendlyFire.On, respawnMode: RespawnMode.Wave);

            var spec = new TeamDeathmatchSpec(10, 60f, 3f, rules);

            Assert.That(spec.Rules, Is.SameAs(rules));
        }
    }
}
