using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GunGameTests
    {
        private const string Rifle = "rifle";
        private const string Launcher = "launcher";
        private const string Handgun = "handgun";
        private const string Knife = "knife";

        private const int Player = 0;
        private const int BotA = 1;
        private const int BotB = 2;

        private static readonly GunGameTier[] _tiers =
        {
            new GunGameTier(Rifle, "Rifle"),
            new GunGameTier(Launcher, "Launcher"),
            new GunGameTier(Handgun, "Handgun"),
        };

        private static readonly string[] _knife = { Knife };
        private static readonly string[] _knifeTwiceAndPadded = { Knife, " knife " };
        private static readonly string[] _blankMelee = { " " };
        private static readonly string[] _nullMelee = { null };

        [Test]
        public void EveryoneStartsOnTheFirstTier()
        {
            var game = new GunGame(Ladder(true));

            Assert.That(game.GetTier(Player), Is.Zero);
            Assert.That(game.GetWeapon(Player).WeaponId, Is.EqualTo(Rifle));
            Assert.That(game.GetNextWeapon(Player)?.WeaponId, Is.EqualTo(Launcher));
            Assert.That(game.FinisherId, Is.Null);
            Assert.That(game.ScoreLimit, Is.Null);
        }

        [Test]
        public void AKillWithTheCurrentWeaponMovesTheKillerToTheNextTier()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            var kill = game.ReportKill(session, Player, BotA, Rifle);

            Assert.That(kill.Promoted, Is.True);
            Assert.That(kill.KillerTierBefore, Is.Zero);
            Assert.That(kill.KillerTier, Is.EqualTo(1));
            Assert.That(kill.Demoted, Is.False);
            Assert.That(kill.Finished, Is.False);
            Assert.That(game.GetTier(Player), Is.EqualTo(1));
            Assert.That(game.GetWeapon(Player).WeaponId, Is.EqualTo(Launcher));
            Assert.That(game.GetTier(BotA), Is.Zero, "a gun kill does not move the victim");
        }

        [Test]
        public void AKillWithAnotherWeaponDoesNotMoveTheKiller()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            Assert.That(game.ReportKill(session, Player, BotA, Launcher).Promoted, Is.False, "a later tier's weapon");
            Assert.That(game.ReportKill(session, Player, BotA, "grenade").Promoted, Is.False, "a weapon off the ladder");
            Assert.That(game.ReportKill(session, Player, BotA, null).Promoted, Is.False, "an unknown weapon");
            Assert.That(game.ReportKill(session, Player, BotA, string.Empty).Promoted, Is.False, "an unknown weapon");
            Assert.That(game.GetTier(Player), Is.Zero);

            Assert.That(game.ReportKill(session, Player, BotA, " rifle ").Promoted, Is.True, "ids are compared trimmed");
        }

        [Test]
        public void AKillWithTheLastWeaponFinishesAndWinsAtTheNextTick()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            game.ReportKill(session, Player, BotA, Rifle);
            game.ReportKill(session, Player, BotB, Launcher);
            Assert.That(game.GetNextWeapon(Player), Is.Null, "the last tier has no next weapon");

            var kill = game.ReportKill(session, Player, BotA, Handgun);

            Assert.That(kill.Finished, Is.True);
            Assert.That(kill.Promoted, Is.True);
            Assert.That(game.FinisherId, Is.EqualTo(Player));
            Assert.That(game.FinisherSide, Is.EqualTo((Side?)new Side(Player)));
            Assert.That(game.GetTier(Player), Is.EqualTo(3));
            Assert.That(game.GetWeaponTier(Player), Is.EqualTo(2), "the finisher keeps the last weapon");
            Assert.That(session.State, Is.EqualTo(GameModeState.Playing), "the mode ends at the head of the next tick");

            session.Tick(0f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(Player)));
        }

        [Test]
        public void OnlyTheFirstFinisherWins()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);
            game.ReportKill(session, Player, BotA, Rifle);
            game.ReportKill(session, Player, BotA, Launcher);
            game.ReportKill(session, BotB, BotA, Rifle);
            game.ReportKill(session, BotB, BotA, Launcher);

            game.ReportKill(session, BotB, Player, Handgun);
            var late = game.ReportKill(session, Player, BotB, Handgun);

            Assert.That(late.Promoted, Is.False);
            Assert.That(late.Finished, Is.False);
            Assert.That(game.FinisherId, Is.EqualTo(BotB));

            session.Tick(0f);
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(BotB)));
        }

        [Test]
        public void AMeleeKillDemotesTheVictimByOneTierWhenEnabled()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);
            game.ReportKill(session, BotA, Player, Rifle);
            game.ReportKill(session, BotA, Player, Launcher);
            Assert.That(game.GetTier(BotA), Is.EqualTo(2));

            var kill = game.ReportKill(session, Player, BotA, Knife);

            Assert.That(kill.Demoted, Is.True);
            Assert.That(kill.VictimTierBefore, Is.EqualTo(2));
            Assert.That(kill.VictimTier, Is.EqualTo(1));
            Assert.That(kill.Promoted, Is.False, "the knife is not the killer's tier weapon");
            Assert.That(game.GetWeapon(BotA).WeaponId, Is.EqualTo(Launcher));
        }

        [Test]
        public void AMeleeKillDoesNotDemoteBelowTheFirstTierOrWhenDisabled()
        {
            var enabled = new GunGame(Ladder(true));
            var session = Started(enabled);
            Assert.That(enabled.ReportKill(session, Player, BotA, Knife).Demoted, Is.False);
            Assert.That(enabled.GetTier(BotA), Is.Zero);

            var disabled = new GunGame(Ladder(false));
            var other = Started(disabled);
            disabled.ReportKill(other, BotA, Player, Rifle);
            Assert.That(disabled.ReportKill(other, Player, BotA, Knife).Demoted, Is.False);
            Assert.That(disabled.GetTier(BotA), Is.EqualTo(1));
        }

        [Test]
        public void AMeleeWeaponOnTheLadderBothPromotesAndDemotes()
        {
            var tiers = new[] { new GunGameTier(Rifle, "Rifle"), new GunGameTier(Knife, "Knife") };
            var game = new GunGame(new GunGameLadder(tiers, _knife, true));
            var session = Started(game);
            game.ReportKill(session, BotA, BotB, Rifle);
            game.ReportKill(session, Player, BotB, Rifle);

            var kill = game.ReportKill(session, Player, BotA, Knife);

            Assert.That(kill.Demoted, Is.True);
            Assert.That(kill.Finished, Is.True);
            Assert.That(game.GetTier(BotA), Is.Zero);
        }

        [Test]
        public void SuicidesTeamKillsAndKillsOutsidePlayDoNotMoveTiers()
        {
            var game = new GunGame(Ladder(true));

            // 味方のキルを作るため、この試合だけ 1 陣営に 2 人入れる。
            var waiting = new ModeSession(new Squads(2, 2), 60f, Array.Empty<IScoreRule>(), new IndividualRespawn(1f), new IWinCondition[] { game });
            waiting.AddPlayer(Player, new Side(0));
            waiting.AddPlayer(BotA, new Side(1));
            waiting.AddPlayer(BotB, new Side(1));

            Assert.That(game.ReportKill(waiting, Player, BotA, Rifle).Promoted, Is.False, "before the start");

            waiting.Start();
            Assert.That(game.ReportKill(waiting, Player, Player, Rifle).Promoted, Is.False, "a suicide");
            Assert.That(game.ReportKill(waiting, BotA, BotB, Rifle).Promoted, Is.False, "a team kill");
            Assert.That(game.ReportKill(waiting, BotB, BotA, Knife).Demoted, Is.False, "a team kill");
            Assert.That(game.GetTier(Player), Is.Zero);
            Assert.That(game.GetTier(BotA), Is.Zero);
        }

        [Test]
        public void ReportKillRejectsUnknownPlayersAndANullSession()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);

            Assert.Throws<ArgumentNullException>(() => game.ReportKill(null, Player, BotA, Rifle));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.ReportKill(session, 9, BotA, Rifle));
            Assert.Throws<ArgumentOutOfRangeException>(() => game.ReportKill(session, Player, 9, Rifle));
        }

        [Test]
        public void TimeUpGoesToTheFurthestTierAndATieIsADraw()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game, 10f);
            game.ReportKill(session, BotA, Player, Rifle);

            session.Tick(10f);

            Assert.That(session.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(session.Winner, Is.EqualTo((Side?)new Side(BotA)));

            var tied = new GunGame(Ladder(true));
            var other = Started(tied, 10f);
            tied.ReportKill(other, BotA, Player, Rifle);
            tied.ReportKill(other, BotB, Player, Rifle);

            other.Tick(10f);

            Assert.That(other.State, Is.EqualTo(GameModeState.Ended));
            Assert.That(other.Winner, Is.Null);
        }

        [Test]
        public void ResetPutsEveryoneBackOnTheFirstTier()
        {
            var game = new GunGame(Ladder(true));
            var session = Started(game);
            game.ReportKill(session, Player, BotA, Rifle);
            game.ReportKill(session, Player, BotA, Launcher);
            game.ReportKill(session, Player, BotA, Handgun);

            game.Reset();

            Assert.That(game.GetTier(Player), Is.Zero);
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
            Assert.That(ladder.IsMelee(Rifle), Is.False);
            Assert.That(ladder.IsMelee(null), Is.False);
            Assert.That(ladder.DemotesOnMelee, Is.False);

            var unnamed = new GunGameTier(" rifle ", " ");
            Assert.That(unnamed.WeaponId, Is.EqualTo(Rifle));
            Assert.That(unnamed.DisplayName, Is.EqualTo(Rifle), "a blank name falls back to the id");
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
            session.AddPlayer(Player, new Side(Player));
            session.AddPlayer(BotA, new Side(BotA));
            session.AddPlayer(BotB, new Side(BotB));
            session.Start();
            return session;
        }
    }
}
