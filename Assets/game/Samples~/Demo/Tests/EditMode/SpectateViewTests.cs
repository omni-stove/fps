using System.Linq;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class SpectateViewTests
    {
        private const string ControlsLine = "[前の武器] 前の味方   [次の武器] 次の味方";

        private Hud _hud;

        [TearDown]
        public void TearDown()
        {
            GameSignals.ResetAll();
            if (_hud != null)
            {
                var settings = _hud.OwnedPanelSettings;
                Object.DestroyImmediate(_hud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }

                _hud = null;
            }
        }

        [Test]
        public void TheHudCarriesTheViewAndHidesItWithoutAValue()
        {
            var view = CreateView();

            Assert.That(_hud.GetComponents<SpectateView>(), Has.Length.EqualTo(1));

            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(view.Element.childCount, Is.EqualTo(0));
        }

        [Test]
        public void APublishedSpectateShowsTheWatchedNameAndTheControls()
        {
            var view = CreateView();
            GameSignals.Participants.Publish(Participants());

            GameSignals.Spectate.Publish(new SpectateStatus(2, 2, SpectateReason.Dead));

            Assert.That(view.IsShown, Is.True);
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(view.Lines, Is.EqualTo(new[] { "P2 を観戦中", ControlsLine }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines));
            Assert.That(view.Element.Children().First().ClassListContains(SpectateView.TargetRowClass), Is.True);
            Assert.That(view.Element.Children().Last().ClassListContains(SpectateView.TargetRowClass), Is.False);

            GameSignals.Spectate.Publish(new SpectateStatus(3, 2, SpectateReason.Dead));
            Assert.That(view.Lines, Is.EqualTo(new[] { "P3 を観戦中", ControlsLine }), "switching to the next teammate renames the line");
        }

        [Test]
        public void TheControlsAreNamedByTheInputActionLabels()
        {
            Assert.That(SpectateText.PreviousActionName, Is.EqualTo("Previous"));
            Assert.That(SpectateText.NextActionName, Is.EqualTo("Next"));
            var fromLabels = SpectateText.Controls(InputActionLabels.LabelOf("Previous"), InputActionLabels.LabelOf("Next"));

            Assert.That(SpectateText.Controls(), Is.EqualTo(fromLabels));
            Assert.That(Localizer.Resolve(SpectateText.Controls()), Is.EqualTo(ControlsLine));
        }

        [Test]
        public void ParticipantsPublishedAfterTheSpectateNameTheTarget()
        {
            var view = CreateView();
            GameSignals.Spectate.Publish(new SpectateStatus(2, 2, SpectateReason.Dead));
            Assert.That(view.Lines[0], Is.EqualTo("プレイヤー 2 を観戦中"), "an Id missing from the participants is named by the Id");

            GameSignals.Participants.Publish(Participants());

            Assert.That(view.Lines[0], Is.EqualTo("P2 を観戦中"));
        }

        [Test]
        public void ASingleTeammateIsShownWithoutControlsAndNoTeammateSaysSo()
        {
            var view = CreateView();
            GameSignals.Participants.Publish(Participants());

            GameSignals.Spectate.Publish(new SpectateStatus(2, 1, SpectateReason.Dead));
            Assert.That(view.Lines, Is.EqualTo(new[] { "P2 を観戦中" }));

            GameSignals.Spectate.Publish(new SpectateStatus(null, 0, SpectateReason.BetweenRounds));
            Assert.That(view.Lines, Is.EqualTo(new[] { "観戦できる味方がいない" }));
            Assert.That(RowTexts(view), Is.EqualTo(view.Lines), "rows left over from the longer lines are taken away");
        }

        [Test]
        public void TheRespawnNoticeAddsTheSecondsAndFollowsTheCountdown()
        {
            var view = CreateView();
            GameSignals.Participants.Publish(Participants());
            var model = new HudModel();
            model.SetRespawnNotice(RespawnMode.Individual, 4);
            GameSignals.HudModel.Publish(model);

            GameSignals.Spectate.Publish(new SpectateStatus(2, 2, SpectateReason.Dead));
            Assert.That(view.Lines, Is.EqualTo(new[] { "P2 を観戦中", ControlsLine, "復活まで 4 秒" }));

            model.SetRespawnNotice(RespawnMode.Individual, 3);
            Assert.That(view.Lines.Last(), Is.EqualTo("復活まで 3 秒"));

            model.SetRespawnNotice(RespawnMode.None, 0);
            Assert.That(view.Lines, Is.EqualTo(new[] { "P2 を観戦中", ControlsLine }), "no seconds without a respawn countdown");
        }

        [Test]
        public void ClearingTheValueHidesTheView()
        {
            var view = CreateView();
            GameSignals.Spectate.Publish(new SpectateStatus(2, 2, SpectateReason.Dead));

            GameSignals.Spectate.Clear();

            Assert.That(view.IsShown, Is.False);
            Assert.That(view.Lines, Is.Empty);
            Assert.That(view.Element.childCount, Is.EqualTo(0));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void RebindingCatchesUpWithTheValuePublishedWhileUnbound()
        {
            var view = CreateView();
            view.Unbind();
            GameSignals.Participants.Publish(Participants());

            GameSignals.Spectate.Publish(new SpectateStatus(3, 1, SpectateReason.Dead));
            Assert.That(view.IsShown, Is.False, "an unbound view does not listen");

            _hud.RebuildView();
            Assert.That(view.Lines, Is.EqualTo(new[] { "P3 を観戦中" }));
            Assert.That(view.Element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void EverySpectateKeyIsInBothLanguages()
        {
            foreach (var language in new[] { GameLanguage.Japanese, GameLanguage.English })
            {
                var table = Localizer.TableOf(language);
                foreach (var key in new[]
                         {
                             SpectateText.TargetKey, SpectateText.NoTargetKey, SpectateText.UnknownTargetKey,
                             SpectateText.ControlsKey, SpectateText.RespawnKey,
                         })
                {
                    Assert.That(table.Get(key), Is.Not.EqualTo(key), language + " " + key);
                }
            }
        }

        private SpectateView CreateView()
        {
            _hud = Hud.Create("SpectateTests.Hud");
            var view = _hud.Spectate;
            Assert.That(view, Is.Not.Null, "Hud.AddViews attaches the spectate view");
            Assert.That(view.Element, Is.Not.Null);
            return view;
        }

        private static ParticipantTable Participants()
        {
            var table = new ParticipantTable();
            table.Set(new Participant(1, "P1", Team.Alpha, true));
            table.Set(new Participant(2, "P2", Team.Alpha, false));
            table.Set(new Participant(3, "P3", Team.Alpha, false));
            return table;
        }

        private static string[] RowTexts(SpectateView view)
        {
            return view.Element.Children().Select(row => ((Label)row).text).ToArray();
        }
    }
}
