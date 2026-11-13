using System;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    /// <summary><see cref="GameRulesDebugDraft"/> がパネルの値から上書き用の <see cref="GameRules"/> を組み立て、変えた項目を書き出すことを確かめる。</summary>
    public sealed class GameRulesDebugDraftTests
    {
        private static readonly string[] _offOn = { "Off", "On" };
        private static readonly string[] _friendlyFireOptions = { "Off", "On", "Reflect" };
        private static readonly string[] _respawnModeOptions = { "Individual", "Wave", "None" };
        private static readonly string[] _deathCameraOptions = { "None", "Killer", "Ally", "Free" };
        private static readonly string[] _hudDensityOptions = { "Full", "Minimal", "Hardcore" };
        private static readonly string[] _downedOptions = { "Off", "Revive" };

        private static readonly string[] _expectedChanges =
        {
            "InfiniteReserve: Off -> On",
            "Downed: Off -> Revive",
            "Cover: Off -> On",
            "HudDensity: Full -> Hardcore",
        };

        // パネルに出さない項目も既定と違う値にしておき、Build がそれらを元のまま残すことまで見分けられるようにする。
        private static readonly ActionRules _actions = new ActionRules(
            Downed.Revive, 20f, 0.5f, 4f, Cover.On, BlindFire.On, ActiveReload.On, Dodge.On, Vault.On, Grenade.On, 2);

        private static readonly GameRules _rules = new GameRules(
            FriendlyFire.Reflect,
            RespawnMode.Wave,
            12f,
            3f,
            7f,
            DeathBody.Collapse,
            DeathCamera.Killer,
            true,
            1.5f,
            Destruction.On,
            _actions,
            2f,
            FallDamage.On,
            HudDensity.Hardcore);

        [Test]
        public void FieldsCoverEveryDefinedFieldInOrder()
        {
            // Unity の .NET には Enum.GetValues<TEnum>() が無いので、dotnet でだけ出る CA2263 を止める
#pragma warning disable CA2263
            var defined = (GameRulesDebugField[])Enum.GetValues(typeof(GameRulesDebugField));
#pragma warning restore CA2263

            Assert.That(GameRulesDebugDraft.Fields, Is.EqualTo(defined));
            Assert.That(defined.Length, Is.EqualTo(15));
        }

        [Test]
        public void NewDraftStartsFromTheBaseRules()
        {
            var draft = new GameRulesDebugDraft(_rules);

            Assert.That(draft.Base, Is.SameAs(_rules));
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                Assert.That(draft.Get(field), Is.EqualTo(GameRulesDebugDraft.Read(_rules, field)), field.ToString());
            }

            Assert.That(draft.Get(GameRulesDebugField.FriendlyFire), Is.EqualTo((int)FriendlyFire.Reflect));
            Assert.That(draft.Get(GameRulesDebugField.InfiniteReserve), Is.EqualTo(1));
            Assert.That(draft.Get(GameRulesDebugField.Cover), Is.EqualTo((int)Cover.On));
        }

        [Test]
        public void ConstructorRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new GameRulesDebugDraft(null));
        }

        [Test]
        public void BuildWithoutEditsKeepsEveryValue()
        {
            var built = new GameRulesDebugDraft(_rules).Build();

            AssertHiddenValuesKept(built);
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                Assert.That(GameRulesDebugDraft.Read(built, field), Is.EqualTo(GameRulesDebugDraft.Read(_rules, field)), field.ToString());
            }

            Assert.That(built.Actions.GrenadeCount, Is.EqualTo(2));
            Assert.That(GameRulesDebugDraft.Changes(_rules, built), Is.Empty);
        }

        [Test]
        public void OptionsMatchTheValuesOfEachField()
        {
            Assert.That(GameRulesDebugDraft.Options(GameRulesDebugField.InfiniteReserve), Is.EqualTo(_offOn));
            Assert.That(GameRulesDebugDraft.Options(GameRulesDebugField.FriendlyFire), Is.EqualTo(_friendlyFireOptions));
            Assert.That(GameRulesDebugDraft.Options(GameRulesDebugField.RespawnMode), Is.EqualTo(_respawnModeOptions));
            Assert.That(GameRulesDebugDraft.Options(GameRulesDebugField.DeathCamera), Is.EqualTo(_deathCameraOptions));
            Assert.That(GameRulesDebugDraft.Options(GameRulesDebugField.HudDensity), Is.EqualTo(_hudDensityOptions));
            Assert.That(GameRulesDebugDraft.Options(GameRulesDebugField.Downed), Is.EqualTo(_downedOptions));
        }

        [Test]
        public void EveryOptionOfEveryFieldSurvivesBuild()
        {
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                var count = GameRulesDebugDraft.Options(field).Count;
                for (var option = 0; option < count; option++)
                {
                    var draft = new GameRulesDebugDraft(GameRules.Default);
                    draft.Set(field, option);

                    var built = draft.Build();

                    Assert.That(GameRulesDebugDraft.Read(built, field), Is.EqualTo(option), $"{field}={option}");
                }
            }
        }

        [Test]
        public void OnlyOffOnPairsAreToggles()
        {
            var toggles = GameRulesDebugDraft.Fields.Where(GameRulesDebugDraft.IsToggle).ToArray();

            Assert.That(toggles, Is.EquivalentTo(new[]
            {
                GameRulesDebugField.InfiniteReserve,
                GameRulesDebugField.Destruction,
                GameRulesDebugField.Downed,
                GameRulesDebugField.Cover,
                GameRulesDebugField.BlindFire,
                GameRulesDebugField.ActiveReload,
                GameRulesDebugField.Dodge,
                GameRulesDebugField.Vault,
                GameRulesDebugField.Grenade,
                GameRulesDebugField.FallDamage,
            }));
        }

        [Test]
        public void SetChangesOnlyThatFieldAndBuildAppliesIt()
        {
            var draft = new GameRulesDebugDraft(_rules);
            draft.Set(GameRulesDebugField.HudDensity, (int)HudDensity.Minimal);
            draft.Set(GameRulesDebugField.InfiniteReserve, 0);
            draft.Set(GameRulesDebugField.Downed, (int)Downed.Off);

            var built = draft.Build();

            Assert.That(built.HudDensity, Is.EqualTo(HudDensity.Minimal));
            Assert.That(built.InfiniteReserve, Is.False);
            Assert.That(built.Actions.Downed, Is.EqualTo(Downed.Off));
            Assert.That(built.FriendlyFire, Is.EqualTo(FriendlyFire.Reflect));
            Assert.That(built.Actions.Cover, Is.EqualTo(Cover.On));
            AssertHiddenValuesKept(built);
            Assert.That(_rules.HudDensity, Is.EqualTo(HudDensity.Hardcore), "base must not change");
        }

        [Test]
        public void TurningBlindFireOnAlsoTurnsCoverOn()
        {
            var draft = new GameRulesDebugDraft(GameRules.Default);

            draft.Set(GameRulesDebugField.BlindFire, (int)BlindFire.On);

            Assert.That(draft.Get(GameRulesDebugField.Cover), Is.EqualTo((int)Cover.On));
            var built = draft.Build();
            Assert.That(built.Actions.BlindFire, Is.EqualTo(BlindFire.On));
            Assert.That(built.Actions.Cover, Is.EqualTo(Cover.On));
        }

        [Test]
        public void TurningCoverOffAlsoTurnsBlindFireOff()
        {
            var draft = new GameRulesDebugDraft(_rules);

            draft.Set(GameRulesDebugField.Cover, (int)Cover.Off);

            Assert.That(draft.Get(GameRulesDebugField.BlindFire), Is.EqualTo((int)BlindFire.Off));
            var built = draft.Build();
            Assert.That(built.Actions.Cover, Is.EqualTo(Cover.Off));
            Assert.That(built.Actions.BlindFire, Is.EqualTo(BlindFire.Off));
        }

        [Test]
        public void TurningCoverOnOrBlindFireOffLeavesTheOtherAlone()
        {
            var draft = new GameRulesDebugDraft(_rules);
            draft.Set(GameRulesDebugField.BlindFire, (int)BlindFire.Off);
            Assert.That(draft.Get(GameRulesDebugField.Cover), Is.EqualTo((int)Cover.On));

            var fromDefault = new GameRulesDebugDraft(GameRules.Default);
            fromDefault.Set(GameRulesDebugField.Cover, (int)Cover.On);
            Assert.That(fromDefault.Get(GameRulesDebugField.BlindFire), Is.EqualTo((int)BlindFire.Off));
        }

        [Test]
        public void TurningGrenadeOnWithNoGrenadesGivesTheDefaultCount()
        {
            var draft = new GameRulesDebugDraft(GameRules.Default);

            draft.Set(GameRulesDebugField.Grenade, (int)Grenade.On);

            Assert.That(draft.Build().Actions.GrenadeCount, Is.EqualTo(GameRulesDebugDraft.DefaultGrenadeCount));
        }

        [Test]
        public void GrenadeCountFromTheBaseIsKept()
        {
            var off = new GameRulesDebugDraft(_rules);
            off.Set(GameRulesDebugField.Grenade, (int)Grenade.Off);
            Assert.That(off.Build().Actions.GrenadeCount, Is.EqualTo(2));

            var on = new GameRulesDebugDraft(_rules.WithActions(_actions.WithGrenade(Grenade.Off)));
            on.Set(GameRulesDebugField.Grenade, (int)Grenade.On);
            Assert.That(on.Build().Actions.GrenadeCount, Is.EqualTo(2));
        }

        [Test]
        public void SetRejectsOptionsOutOfRangeAndKeepsTheValue()
        {
            var draft = new GameRulesDebugDraft(_rules);

            Assert.Throws<ArgumentOutOfRangeException>(() => draft.Set(GameRulesDebugField.FriendlyFire, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => draft.Set(GameRulesDebugField.FriendlyFire, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => draft.Set(GameRulesDebugField.InfiniteReserve, 2));
            Assert.That(draft.Get(GameRulesDebugField.FriendlyFire), Is.EqualTo((int)FriendlyFire.Reflect));
            Assert.That(draft.Get(GameRulesDebugField.InfiniteReserve), Is.EqualTo(1));
        }

        [Test]
        public void UndefinedFieldsAreRejected()
        {
            var draft = new GameRulesDebugDraft(_rules);
            var undefined = (GameRulesDebugField)99;

            Assert.Throws<ArgumentOutOfRangeException>(() => draft.Get(undefined));
            Assert.Throws<ArgumentOutOfRangeException>(() => draft.Set(undefined, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameRulesDebugDraft.Read(_rules, undefined));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameRulesDebugDraft.Options(undefined));
        }

        [Test]
        public void ChangesListEditedFieldsInPanelOrder()
        {
            var draft = new GameRulesDebugDraft(GameRules.Default);
            draft.Set(GameRulesDebugField.HudDensity, (int)HudDensity.Hardcore);
            draft.Set(GameRulesDebugField.Cover, (int)Cover.On);
            draft.Set(GameRulesDebugField.InfiniteReserve, 1);
            draft.Set(GameRulesDebugField.Downed, (int)Downed.Revive);

            var changes = GameRulesDebugDraft.Changes(GameRules.Default, draft.Build());

            Assert.That(changes, Is.EqualTo(_expectedChanges));
        }

        [Test]
        public void ChangesIncludeTheGrenadeCountGivenOnTurningGrenadesOn()
        {
            var draft = new GameRulesDebugDraft(GameRules.Default);
            draft.Set(GameRulesDebugField.Grenade, (int)Grenade.On);

            var changes = GameRulesDebugDraft.Changes(GameRules.Default, draft.Build());

            Assert.That(changes, Is.EqualTo(new[]
            {
                "Grenade: Off -> On",
                "GrenadeCount: 0 -> " + GameRulesDebugDraft.DefaultGrenadeCount,
            }));
        }

        [Test]
        public void ChangesRejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => GameRulesDebugDraft.Changes(null, _rules));
            Assert.Throws<ArgumentNullException>(() => GameRulesDebugDraft.Changes(_rules, null));
        }

        [Test]
        public void ApplyLogNamesTheChangedFieldsWithTheTag()
        {
            var draft = new GameRulesDebugDraft(GameRules.Default);
            draft.Set(GameRulesDebugField.Cover, (int)Cover.On);
            draft.Set(GameRulesDebugField.FriendlyFire, (int)FriendlyFire.On);

            var log = GameRulesDebugDraft.FormatApplyLog(GameRules.Default, draft.Build());

            Assert.That(log, Is.EqualTo("[RulesDebug] apply: FriendlyFire: Off -> On, Cover: Off -> On"));
        }

        [Test]
        public void ApplyLogSaysWhenNothingChanged()
        {
            var log = GameRulesDebugDraft.FormatApplyLog(_rules, new GameRulesDebugDraft(_rules).Build());

            Assert.That(log, Is.EqualTo("[RulesDebug] apply: no changes"));
        }

        [Test]
        public void ResetLogHasTheTag()
        {
            Assert.That(GameRulesDebugDraft.FormatResetLog(), Does.StartWith(GameRulesDebugDraft.LogTag + " reset"));
        }

        private static void AssertHiddenValuesKept(GameRules built)
        {
            Assert.That(built.WaveIntervalSeconds, Is.EqualTo(12f));
            Assert.That(built.RegenDelaySeconds, Is.EqualTo(3f));
            Assert.That(built.RegenPerSecond, Is.EqualTo(7f));
            Assert.That(built.ReserveAmmoMultiplier, Is.EqualTo(1.5f));
            Assert.That(built.SpawnProtectionSeconds, Is.EqualTo(2f));
            Assert.That(built.Actions.DownedBleedoutSeconds, Is.EqualTo(20f));
            Assert.That(built.Actions.DownedHealthFraction, Is.EqualTo(0.5f));
            Assert.That(built.Actions.ReviveSeconds, Is.EqualTo(4f));
        }
    }
}
