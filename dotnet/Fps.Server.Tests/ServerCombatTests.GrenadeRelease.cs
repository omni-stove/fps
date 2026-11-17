using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed partial class ServerCombatTests
    {
        private static readonly GrenadeSpec _farRelease = new GrenadeSpec(releaseDistance: 2f, bodyRadius: 0.2f);

        [Test]
        public void TheServerReleasesTheGrenadeAtTheSpecReleaseDistance()
        {
            Start(Rules(), grenades: GrenadeCatalog.Of(_farRelease));
            Join(1);
            Join(2);
            Step(0.2f);
            var eye = Body(1).EyePosition;

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX, GrenadeTypeId = _farRelease.Id });

            var released = Snapshot(2).Grenades[0].Position;
            Assert.That(released.X, Is.EqualTo(eye.X + 2f).Within(1e-4f));
            Assert.That(released.Y, Is.EqualTo(eye.Y).Within(1e-4f));
        }

        [Test]
        public void AWallInsideTheReleaseDistanceKeepsTheSpecBallOutOfIt()
        {
            var reachesTheWall = new GrenadeSpec(releaseDistance: 6f, bodyRadius: 0.2f);
            Start(Rules(), wall: true, grenades: GrenadeCatalog.Of(reachesTheWall));
            Join(1);
            Join(2);
            Step(0.2f);
            var eye = Body(1).EyePosition;

            _host.OnMessage(new ConnectionId(1), new ThrowGrenadeRequestMessage { Direction = Vector3.UnitX, GrenadeTypeId = reachesTheWall.Id });

            // 壁の手前の面は x=-0.25。半径 0.2 の球が埋まらないよう x=-0.45 に出す
            Assert.That(Snapshot(2).Grenades[0].Position.X, Is.EqualTo(-0.45f).Within(1e-3f));
            Assert.That(eye.X + 6f, Is.GreaterThan(-0.25f), "the release distance alone would put it past the wall");
        }
    }
}
