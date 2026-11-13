using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GunGameTests
    {
        private const string _rifle = "rifle";
        private const string _launcher = "launcher";
        private const string _handgun = "handgun";
        private const string _knifeId = "knife";

        private const int _player = 0;
        private const int _botA = 1;
        private const int _botB = 2;

        private static readonly GunGameTier[] _tiers =
        {
            new GunGameTier(_rifle, "Rifle"),
            new GunGameTier(_launcher, "Launcher"),
            new GunGameTier(_handgun, "Handgun"),
        };

        private static readonly string[] _knife = { _knifeId };
        private static readonly string[] _knifeTwiceAndPadded = { _knifeId," knife " };
        private static readonly string[] _blankMelee = { " " };
        private static readonly string[] _nullMelee = { null };

        [Test]
        public void EveryoneStartsOnTheFirstTier()
        {
            var game = new GunGame(Ladder(true));

            Assert.That(game.GetTier(_player), Is.Zero);
            Assert.That(game.GetWeapon(_player).WeaponId, Is.EqualTo(_rifle));
            Assert.That(game.GetNextWeapon(_player)?.WeaponId, Is.EqualTo(_launcher));
            Assert.That(game.FinisherId, Is.Null);
            Assert.That(game.ScoreLimit, Is.Null);
        }

        [Test]
        public void AKillWithTheCurrentWeaponMovesTheKillerToTheNextTier()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            var kill = game.ReportKill(session, _player, _botA, _rifle);

            Assert.That(kill.Promoted, Is.True);
            Assert.That(kill.KillerTierBefore, Is.Zero);
            Assert.That(kill.KillerTier, Is.EqualTo(1));
            Assert.That(kill.Demoted, Is.False);
            Assert.That(kill.Finished, Is.False);
            Assert.That(game.GetTier(_player), Is.EqualTo(1));
            Assert.That(game.GetWeapon(_player).WeaponId, Is.EqualTo(_launcher));
            Assert.That(game.GetTier(_botA), Is.Zero, "a gun kill does not move the victim");
        }

        [Test]
        public void AKillWithAnotherWeaponDoesNotMoveTheKiller()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            Assert.That(game.ReportKill(session, _player, _botA, _launcher).Promoted, Is.False, "a later tier's weapon");
            Assert.That(game.ReportKill(session, _player, _botA, "grenade").Promoted, Is.False, "a weapon off the ladder");
            Assert.That(game.ReportKill(session, _player, _botA, null).Promoted, Is.False, "an unknown weapon");
            Assert.That(game.ReportKill(session, _player, _botA, string.Empty).Promoted, Is.False, "an unknown weapon");
            Assert.That(game.GetTier(_player), Is.Zero);

            Assert.That(game.ReportKill(session, _player, _botA, " rifle ").Promoted, Is.True, "ids are compared trimmed");
        }

        [Test]
        public void AKillWithTheLastWeaponFinishesAndWinsAtTheNextTick()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            game.ReportKill(session, _player, _botA, _rifle);
            game.ReportKill(session, _player, _botB, _launcher);
            Assert.That(game.GetNextWeapon(_player), Is.Null, "the last tier has no next weapon");

            var kill = game.ReportKill(session, _player, _botA, _handgun);

            Assert.That(kill.Finished, Is.True);
            Assert.That(kill.Promoted, Is.True);
            Assert.That(game.FinisherId, Is.EqualTo(_player));
            Assert.That(game.FinisherSide, Is.EqualTo((Side?)new Side(_player)));
            Assert.That(game.GetTier(_player), Is.EqualTo(3));
            Assert.That(game.GetWeaponTier(_player), Is.EqualTo(2), "the finisher keeps the last weapon");
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing), "the mode ends at the head of the next tick");

            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(_player)));
        }

        [Test]
        public void OnlyTheFirstFinisherWins()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);
            game.ReportKill(session, _player, _botA, _rifle);
            game.ReportKill(session, _player, _botA, _launcher);
            game.ReportKill(session, _botB, _botA, _rifle);
            game.ReportKill(session, _botB, _botA, _launcher);

            game.ReportKill(session, _botB, _player, _handgun);
            var late = game.ReportKill(session, _player, _botB, _handgun);

            Assert.That(late.Promoted, Is.False);
            Assert.That(late.Finished, Is.False);
            Assert.That(game.FinisherId, Is.EqualTo(_botB));

            session.Tick(0f);
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(_botB)));
        }

        [Test]
        public void AMeleeKillDemotesTheVictimByOneTierWhenEnabled()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);
            game.ReportKill(session, _botA, _player, _rifle);
            game.ReportKill(session, _botA, _player, _launcher);
            Assert.That(game.GetTier(_botA), Is.EqualTo(2));

            var kill = game.ReportKill(session, _player, _botA, _knifeId);

            Assert.That(kill.Demoted, Is.True);
            Assert.That(kill.VictimTierBefore, Is.EqualTo(2));
            Assert.That(kill.VictimTier, Is.EqualTo(1));
            Assert.That(kill.Promoted, Is.False, "the knife is not the killer's tier weapon");
            Assert.That(game.GetWeapon(_botA).WeaponId, Is.EqualTo(_launcher));
        }

        [Test]
        public void AMeleeKillDoesNotDemoteBelowTheFirstTierOrWhenDisabled()
        {
            var enabled = new GunGame(Ladder(true));
            var session = Started(enabled);
            Assert.That(enabled.ReportKill(session, _player, _botA, _knifeId).Demoted, Is.False);
            Assert.That(enabled.GetTier(_botA), Is.Zero);

            var disabled = new GunGame(Ladder(false));
            var other = Started(disabled);
            disabled.ReportKill(other, _botA, _player, _rifle);
            Assert.That(disabled.ReportKill(other, _player, _botA, _knifeId).Demoted, Is.False);
            Assert.That(disabled.GetTier(_botA), Is.EqualTo(1));
        }

        [Test]
        public void AMeleeWeaponOnTheLadderBothPromotesAndDemotes()
        {
            var tiers = new[] { new GunGameTier(_rifle, "Rifle"), new GunGameTier(_knifeId, "Knife") };
            var game = new GunGame(new GunGameLadder(tiers, _knife, true));
            var session = Started(game);
            game.ReportKill(session, _botA, _botB, _rifle);
            game.ReportKill(session, _player, _botB, _rifle);

            var kill = game.ReportKill(session, _player, _botA, _knifeId);

            Assert.That(kill.Demoted, Is.True);
            Assert.That(kill.Finished, Is.True);
            Assert.That(game.GetTier(_botA), Is.Zero);
        }

        [Test]
        public void SuicidesTeamKillsAndKillsOutsidePlayDoNotMoveTiers()
        {
            var game = new GunGame(Ladder(true));

            // 味方のキルを作るため、この試合だけ 1 陣営に 2 人入れる。
            var waiting = new ModeSession(new Squads(2, 2), 60f, Array.Empty<IScoreRule>(), new IndividualRespawn(1f), new IWinCondition[] { game });
            waiting.AddPlayer(_player, new Side(0));
            waiting.AddPlayer(_botA, new Side(1));
            waiting.AddPlayer(_botB, new Side(1));

            Assert.That(game.ReportKill(waiting, _player, _botA, _rifle).Promoted, Is.False, "before the start");

            waiting.Start();
            Assert.That(game.ReportKill(waiting, _player, _player, _rifle).Promoted, Is.False, "a suicide");
            Assert.That(game.ReportKill(waiting, _botA, _botB, _rifle).Promoted, Is.False, "a team kill");
            Assert.That(game.ReportKill(waiting, _botB, _botA, _knifeId).Demoted, Is.False, "a team kill");
            Assert.That(game.GetTier(_player), Is.Zero);
            Assert.That(game.GetTier(_botA), Is.Zero);
        }

        [Test]
        public void ReportKillRejectsUnknownPlayersAndANullSession()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            Assert.Throws<ArgumentNullException>(() => game.ReportKill(null, _player, _botA, _rifle));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.ReportKill(session, 9, _botA, _rifle));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.ReportKill(session, _player, 9, _rifle));
        }

        [Test]
        public void TimeUpGoesToTheFurthestTierAndATieIsADraw()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game, 10f);
            game.ReportKill(session, _botA, _player, _rifle);

            session.Tick(10f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(_botA)));

            var tied = new GunGame(Ladder(true));
            var other = Started(tied, 10f);
            tied.ReportKill(other, _botA, _player, _rifle);
            tied.ReportKill(other, _botB, _player, _rifle);

            other.Tick(10f);

            Assert.That(other.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(other.Winner, Is.Null);
        }

        [Test]
        public void ResetPutsEveryoneBackOnTheFirstTier()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);
            game.ReportKill(session, _player, _botA, _rifle);
            game.ReportKill(session, _player, _botA, _launcher);
            game.ReportKill(session, _player, _botA, _handgun);

            game.Reset();

            Assert.That(game.GetTier(_player), Is.Zero);
            Assert.That(game.FinisherId, Is.Null);
            Assert.That(game.CheckStanding(session).IsDecided, Is.False);
        }

        [Test]
        public void TheLadderValidatesItsTiersAndMeleeIds()
        {
            Assert.Throws<ArgumentNullException>(() => new GunGameLadder(null, null, true));
            Assert.Throws<ArgumentException>(() => new GunGameLadder(Array.Empty<GunGameTier>(), null, true));
            Assert.Throws<ArgumentException>(() => new GunGameLadder(new[] { default(GunGameTier) }, null, true));
            Assert.Throws<ArgumentException>(() => new GunGameLadder(_tiers, _blankMelee, true));
            Assert.Throws<ArgumentException>(() => new GunGameLadder(_tiers, _nullMelee, true));
            Assert.Throws<ArgumentException>(() => new GunGameTier(" ", "Blank"));
            Assert.Throws<ArgumentNullException>(() => new GunGame(null));

            var ladder = new GunGameLadder(_tiers, _knifeTwiceAndPadded, false);
            Assert.That(ladder.TierCount, Is.EqualTo(3));
            Assert.That(ladder.MeleeWeaponIds, Is.EqualTo(_knife));
            Assert.That(ladder.IsMelee(" knife"), Is.True);
            Assert.That(ladder.IsMelee(_rifle), Is.False);
            Assert.That(ladder.IsMelee(null), Is.False);
            Assert.That(ladder.DemotesOnMelee, Is.False);

            var unnamed = new GunGameTier(" rifle ", " ");
            Assert.That(unnamed.WeaponId, Is.EqualTo(_rifle));
            Assert.That(unnamed.DisplayName, Is.EqualTo(_rifle), "a blank name falls back to the id");
        }

        [Test]
        public void TheHudAndLogTextCountTiersFromOne()
        {
            Assert.That(GunGameText.Tier(1, 3, "Launcher"), Is.EqualTo("段 2/3 Launcher"));
            Assert.That(GunGameText.Tier(3, 3, "Handgun"), Is.EqualTo("段 3/3 Handgun"), "the finisher stays on the last tier");
            Assert.That(GunGameText.Next("Handgun"), Is.EqualTo("次: Handgun"));
            Assert.That(GunGameText.Next(null), Is.EqualTo("次: なし (この武器で倒せば勝ち)"));
            Assert.That(GunGameText.Promote(0, 1, 3, "Launcher"), Is.EqualTo("[Mode] gungame promote player=0 tier=2/3 weapon=Launcher"));
            Assert.That(GunGameText.Demote(2, 0, 3, "Rifle"), Is.EqualTo("[Mode] gungame demote player=2 tier=1/3 weapon=Rifle"));
            Assert.That(GunGameText.Equip(1, 0, 3, string.Empty), Is.EqualTo("[Mode] gungame equip player=1 tier=1/3 weapon=-"));
            Assert.That(GunGameText.Finish(0, "Handgun"), Is.EqualTo("[Mode] gungame finish player=0 weapon=Handgun"));
        }

        private static GunGameLadder Ladder(bool demotesOnMelee)
        {
            return new GunGameLadder(_tiers, _knife, demotesOnMelee);
        }

        private static ModeSession Session(GunGame game, float timeLimitSeconds = 60f)
        {
            return new ModeSession(
                new FreeForAll(),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore() },
                new IndividualRespawn(1f),
                new IWinCondition[] { game });
        }

        private static ModeSession Started(GunGame game, float timeLimitSeconds = 60f)
        {
            var session = Session(game, timeLimitSeconds);
            session.AddPlayer(_player, new Side(_player));
            session.AddPlayer(_botA, new Side(_botA));
            session.AddPlayer(_botB, new Side(_botB));
            session.Start();
            return session;
        }
    }
}
