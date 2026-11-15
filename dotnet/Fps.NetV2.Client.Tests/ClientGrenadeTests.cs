using System.Collections.Generic;
using System.Numerics;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    public sealed class ClientGrenadeTests
    {
        private const string _frag = "ff94f232-2647-4b77-a077-5693d8f954d1";
        private const string _smoke = "ecaf6e57-cb77-4707-9321-6ea5b659408f";
        private static readonly string[] _startUpdateEnd = { "start 3 smoke", "update 3", "end 3" };

        [Test]
        public void AnAreaIsStartedUpdatedAndEnded()
        {
            var areas = new ClientAreaEffects();
            var log = new List<string>();
            areas.Started += s => log.Add("start " + s.AreaId + " " + s.KindId);
            areas.Updated += s => log.Add("update " + s.AreaId);
            areas.Ended += s => log.Add("end " + s.AreaId);

            areas.Receive(Area(3, 10f));
            Assert.That(areas.TryGet(3, out var state), Is.True);
            Assert.That(state.Radius, Is.EqualTo(5f));
            Assert.That(state.Center, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            areas.Receive(Area(3, 4f));
            areas.Receive(Area(3, 0f));

            Assert.That(log, Is.EqualTo(_startUpdateEnd));
            Assert.That(areas.Count, Is.Zero);
        }

        [Test]
        public void AnEndForAnUnknownAreaIsIgnored()
        {
            var areas = new ClientAreaEffects();
            var ended = 0;
            areas.Ended += _ => ended++;

            areas.Receive(Area(7, 0f));

            Assert.That(ended, Is.Zero);
        }

        [Test]
        public void ClearEndsEveryArea()
        {
            var areas = new ClientAreaEffects();
            var ended = 0;
            areas.Ended += _ => ended++;
            areas.Receive(Area(1, 5f));
            areas.Receive(Area(2, 5f));

            areas.Clear();

            Assert.That(ended, Is.EqualTo(2));
            Assert.That(areas.Count, Is.Zero);
        }

        [Test]
        public void TheFirstCarriedTypeIsThrownUntilAnotherIsSelected()
        {
            var grenades = new ClientGrenades();
            var pouch = Pouch(_frag, _smoke);

            Assert.That(grenades.Current(pouch), Is.EqualTo(_frag));
            Assert.That(grenades.SelectNext(pouch), Is.True);
            Assert.That(grenades.Current(pouch), Is.EqualTo(_smoke));
            Assert.That(grenades.SelectNext(pouch), Is.True);
            Assert.That(grenades.Current(pouch), Is.EqualTo(_frag));
        }

        [Test]
        public void ASelectionThatIsNoLongerCarriedFallsBackToTheFirst()
        {
            var grenades = new ClientGrenades();
            grenades.SelectNext(Pouch(_frag, _smoke));

            Assert.That(grenades.Current(Pouch(_frag)), Is.EqualTo(_frag));
            Assert.That(grenades.SelectNext(Pouch(_frag)), Is.False);
            Assert.That(grenades.Current(new GrenadePouchSync()), Is.Empty);
            Assert.That(grenades.Current(null), Is.Empty);
        }

        private static AreaEffectMessage Area(int id, float remaining)
        {
            return new AreaEffectMessage { AreaId = id, KindId = "smoke", Center = new Vector3(1f, 0f, 2f), Radius = 5f, RemainingSeconds = remaining };
        }

        private static GrenadePouchSync Pouch(params string[] typeIds)
        {
            var pouch = new GrenadePouchSync { Enabled = true };
            foreach (var id in typeIds)
            {
                pouch.Add(id, 2);
            }

            return pouch;
        }
    }
}
