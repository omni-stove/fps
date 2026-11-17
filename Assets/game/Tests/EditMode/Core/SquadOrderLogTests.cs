using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class SquadOrderLogTests
    {
        [Test]
        public void LogsAreTaggedAndReadable()
        {
            Assert.That(SquadOrderLog.Spec(SquadOrderSpec.Default), Is.EqualTo("[Squad] spec hold=0.50 distance=150.0 nearby=30.0 follow=3.0 leash=15.0"));
            Assert.That(
                SquadOrderLog.Issued(BotErrandKind.Hold, null, new Vector3(1f, 0f, 10f), 2, false),
                Is.EqualTo("[Squad] order kind=Hold target=none at=(1.00, 0.00, 10.00) recipients=2 squads=false"));
            Assert.That(SquadOrderLog.Refused(SquadOrderRefusal.NoRecipients), Is.EqualTo("[Squad] refused reason=NoRecipients"));
            Assert.That(
                SquadOrderLog.Accepted("Bot 1", BotErrandKind.Attack, "Dummy_0", new Vector3(1f, 0f, 10f)),
                Is.EqualTo("[Squad] accepted id=Bot_1 kind=Attack target=Dummy_0 at=(1.00, 0.00, 10.00)"));
            Assert.That(SquadOrderLog.Arrived("Bot_1", Vector3.Zero), Is.EqualTo("[Squad] arrived id=Bot_1 at=(0.00, 0.00, 0.00)"));
            Assert.That(SquadOrderLog.Engage("Bot_1", "Dummy_0"), Is.EqualTo("[Squad] engage id=Bot_1 target=Dummy_0"));
            Assert.That(
                SquadOrderLog.Done("Bot_1", BotErrandKind.Attack, "TargetGone", BotErrandKind.Follow),
                Is.EqualTo("[Squad] done id=Bot_1 kind=Attack reason=TargetGone next=Follow"));
            Assert.That(
                SquadOrderLog.Screen(BotErrandKind.Hold, new PingScreenPlacement(true, 0.5f, 0.62f, 0f, PingScreenEdge.None)),
                Is.EqualTo("[Squad] screen kind=Hold onScreen=true edge=None at=(0.50, 0.62)"));
            Assert.That(SquadOrderLog.View("SquadOrderMarkerView"), Is.EqualTo("[Squad] view=SquadOrderMarkerView"));
        }

        [Test]
        public void TheSpecRejectsBadValues()
        {
            Assert.That(SquadOrderSpec.Default.Aim.MaxDistance, Is.EqualTo(SquadOrderSpec.DefaultMaxDistance));
            Assert.That(SquadOrderSpec.Default.Aim.EnemyAimToleranceDegrees, Is.EqualTo(SquadOrderSpec.DefaultEnemyAimToleranceDegrees));

            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderSpec(holdSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderSpec(maxDistance: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderSpec(enemyAimToleranceDegrees: 90f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderSpec(nearbyRadius: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderSpec(followDistance: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SquadOrderSpec(followDistance: 5f, leashDistance: 4f));
        }
    }
}
