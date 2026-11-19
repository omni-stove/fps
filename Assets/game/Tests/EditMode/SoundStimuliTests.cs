using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class SoundStimuliTests
    {
        private readonly List<Action<GameObject, SoundStimulus>> _added = new List<Action<GameObject, SoundStimulus>>();

        [TearDown]
        public void TearDown()
        {
            foreach (var handler in _added)
            {
                SoundStimuli.Emitted -= handler;
            }

            _added.Clear();
        }

        [Test]
        public void ASubscriberThatThrowsDoesNotStopTheOnesAfterIt()
        {
            var heard = new List<string>();
            var source = new GameObject("Shooter");
            try
            {
                var shot = new SoundStimulus(SoundCueKind.Gunshot, new System.Numerics.Vector3(1f, 0f, 2f), 30f, false);
                Add((from, sound) => heard.Add("first:" + from.name));
                Add((from, sound) => throw new InvalidOperationException("listener broke"));
                Add((from, sound) => heard.Add("last:" + sound.Kind));

                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: listener broke"));
                SoundStimuli.Emit(source, shot);

                Assert.That(heard, Is.EqualTo(new[] { "first:Shooter", "last:Gunshot" }));
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void EmittingWithNoSubscribersDoesNothing()
        {
            var shot = new SoundStimulus(SoundCueKind.Gunshot, System.Numerics.Vector3.Zero, 30f, false);

            Assert.DoesNotThrow(() => SoundStimuli.Emit(null, shot));
        }

        [Test]
        public void TheSourceAndSoundArePassedAsIs()
        {
            GameObject seenSource = null;
            SoundStimulus seenSound = default;
            var source = new GameObject("Thrower");
            try
            {
                var clatter = new SoundStimulus(SoundCueKind.Clatter, new System.Numerics.Vector3(0f, 0f, -4f), 10f, true);
                Add((from, sound) =>
                {
                    seenSource = from;
                    seenSound = sound;
                });

                SoundStimuli.Emit(source, clatter);

                Assert.That(seenSource, Is.SameAs(source));
                Assert.That(seenSound.Kind, Is.EqualTo(SoundCueKind.Clatter));
                Assert.That(seenSound.Position, Is.EqualTo(clatter.Position));
                Assert.That(seenSound.Draws, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        private void Add(Action<GameObject, SoundStimulus> handler)
        {
            _added.Add(handler);
            SoundStimuli.Emitted += handler;
        }
    }
}
