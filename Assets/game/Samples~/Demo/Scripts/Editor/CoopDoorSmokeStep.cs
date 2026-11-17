using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Editor
{
    // batch mode では Interact のキーを押せないので、プレイヤーも Bot の代わりも CoopInteractable.BeginHold で押す。
    public sealed class CoopDoorSmokeStep : ITpsActionSmokeStep
    {
        public const int StepOrder = CoopDoorRangeFixture.PlaceOrder;

        public const string PartnerNamePrefix = "CoopPartner";

        private const float SoloSeconds = 1f;

        private const float StallSeconds = 1f;

        // 済む前に確実に離すため、押し続ける秒数のこの割合まで進んだところで離す。
        private const float LeaveAtProgress = 0.3f;

        private const float ExtraSeconds = 10f;

        private const float ProgressTolerance = 1e-4f;

        private const float PositionTolerance = 0.01f;

        private readonly List<string> _logs = new List<string>();
        private readonly List<GameObject> _partners = new List<GameObject>();

        private Phase _phase;
        private BootstrapWorld _world;
        private CoopDoor _door;
        private CoopInteractable _interactable;
        private Vector3 _panelClosed;
        private float _waitUntil;
        private float _stalledProgress;

        private enum Phase
        {
            Start,
            Solo,
            WaitStarted,
            WaitInterrupted,
            Stalled,
            WaitOpened,
            Done,
        }

        public int Order => StepOrder;

        public string Name => "coop-door";

        // ExtraSteps が手順を集める時点ではシーンが無く、置いた扉の設定を読めないので既定値で見積もる。
        public float TimeoutSeconds =>
            SoloSeconds + CoopHoldSpec.DefaultHoldSeconds + StallSeconds + CoopDoor.DefaultOpenSeconds + ExtraSeconds;

        public void Reset()
        {
            _logs.Clear();
            _partners.Clear();
            _phase = Phase.Start;
            _world = null;
            _door = null;
            _interactable = null;
            _panelClosed = Vector3.zero;
            _waitUntil = 0f;
            _stalledProgress = 0f;
        }

        public void OnLog(string condition)
        {
            if (condition != null && condition.StartsWith(CoopHoldLog.Tag + " ", System.StringComparison.Ordinal))
            {
                _logs.Add(condition);
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            switch (_phase)
            {
                case Phase.Start:
                    problem = Begin(world);
                    break;
                case Phase.Solo:
                    problem = CheckSolo();
                    break;
                case Phase.WaitStarted:
                    problem = CheckStarted();
                    break;
                case Phase.WaitInterrupted:
                    problem = CheckInterrupted();
                    break;
                case Phase.Stalled:
                    problem = CheckStalled();
                    break;
                case Phase.WaitOpened:
                    problem = CheckOpened();
                    break;
            }

            return problem == null && _phase == Phase.Done;
        }

        public void Cleanup()
        {
            if (_interactable != null && _world != null)
            {
                _interactable.EndHold(_world.Player);
                foreach (var partner in _partners)
                {
                    _interactable.EndHold(partner);
                }
            }

            foreach (var partner in _partners)
            {
                // Play を抜けたあとなら、作った物はシーンごと消えている。
                if (partner != null && Application.isPlaying)
                {
                    Object.Destroy(partner);
                }
            }

            _partners.Clear();
            _interactable = null;
            _door = null;
            _world = null;
        }

        private string Begin(BootstrapWorld world)
        {
            _world = world;
            _door = Object.FindAnyObjectByType<CoopDoor>();
            if (_door == null)
            {
                return $"the scene has no {nameof(CoopDoor)}; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            _interactable = _door.Interactable;
            if (_interactable == null || _door.Panel == null)
            {
                return $"the door '{_door.name}' is not wired to a {nameof(CoopInteractable)} and a panel";
            }

            if (_interactable.RequiredCount < 2)
            {
                return $"the door '{_door.name}' needs {Count(_interactable.RequiredCount)} holder, so holding alone cannot be checked to stall";
            }

            if (!(_interactable.HoldSeconds > 0f))
            {
                return $"the door '{_door.name}' completes as soon as the holders gather, so an interruption cannot be checked";
            }

            if (_door.IsOpening || _interactable.IsCompleted)
            {
                return $"the door '{_door.name}' was already opened before the smoke";
            }

            _panelClosed = _door.Panel.localPosition;
            Teleport(world.Controller, CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.StandPoint));
            for (var i = 1; i < _interactable.RequiredCount; i++)
            {
                var partner = new GameObject(PartnerNamePrefix + Count(i));
                partner.transform.position = CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.PartnerPoint);
                _partners.Add(partner);
            }

            Physics.SyncTransforms();
            if (!_interactable.BeginHold(world.Player))
            {
                return $"the player at {Format(world.Player.transform.position)} could not hold '{_interactable.name}'";
            }

            _waitUntil = Time.time + SoloSeconds;
            _phase = Phase.Solo;
            return null;
        }

        private string CheckSolo()
        {
            if (_interactable.IsRunning || _interactable.Progress > 0f)
            {
                return $"'{_interactable.name}' progressed with only the player holding ({Describe()})";
            }

            if (Time.time < _waitUntil)
            {
                return null;
            }

            var alone = Own($"holding 1/{Count(_interactable.RequiredCount)}");
            if (!HasLog(alone) || HasLog(Own("started ")))
            {
                return $"holding alone did not log '{alone}' without starting (logs: {string.Join(" | ", _logs)})";
            }

            foreach (var partner in _partners)
            {
                if (!_interactable.BeginHold(partner))
                {
                    return $"the stand-in bot '{partner.name}' at {Format(partner.transform.position)} could not hold '{_interactable.name}'";
                }
            }

            _phase = Phase.WaitStarted;
            return null;
        }

        private string CheckStarted()
        {
            if (_interactable.IsCompleted)
            {
                return $"'{_interactable.name}' completed before a holder could leave ({Describe()})";
            }

            if (!HasLog(Own("started ")) || _interactable.Progress < LeaveAtProgress)
            {
                return null;
            }

            var required = Count(_interactable.RequiredCount);
            var gathered = Own($"holding {required}/{required}");
            if (!HasLog(gathered))
            {
                return $"gathering every holder did not log '{gathered}'";
            }

            _partners[0].transform.position = CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.OutPoint);
            Physics.SyncTransforms();
            _phase = Phase.WaitInterrupted;
            return null;
        }

        private string CheckInterrupted()
        {
            if (_interactable.IsCompleted)
            {
                return $"'{_interactable.name}' completed after '{_partners[0].name}' left ({Describe()})";
            }

            if (!HasLog(Own("interrupted ")))
            {
                return null;
            }

            var left = Own($"hold end holder={_partners[0].name} reason=left");
            if (!HasLog(left))
            {
                return $"leaving the range did not log '{left}'";
            }

            _stalledProgress = _interactable.Progress;
            _waitUntil = Time.time + StallSeconds;
            _phase = Phase.Stalled;
            return null;
        }

        private string CheckStalled()
        {
            if (_interactable.IsRunning || _interactable.Progress > _stalledProgress + ProgressTolerance || _door.IsOpening)
            {
                return $"'{_interactable.name}' kept progressing after '{_partners[0].name}' left ({Describe()})";
            }

            if (Time.time < _waitUntil)
            {
                return null;
            }

            _partners[0].transform.position = CoopDoorRangeFixture.BodyAt(CoopDoorRangeFixture.PartnerPoint);
            Physics.SyncTransforms();
            if (!_interactable.BeginHold(_partners[0]))
            {
                return $"'{_partners[0].name}' could not hold '{_interactable.name}' again after coming back";
            }

            _phase = Phase.WaitOpened;
            return null;
        }

        private string CheckOpened()
        {
            if (!_door.IsOpen)
            {
                return null;
            }

            if (!HasLog(Own("completed ")) || !HasLog(OfDoor("opening")) || !HasLog(OfDoor("opened")))
            {
                return $"opening the door did not log completed, opening and opened (logs: {string.Join(" | ", _logs)})";
            }

            var expected = _panelClosed + _door.OpenOffset;
            if (Vector3.Distance(_door.Panel.localPosition, expected) > PositionTolerance)
            {
                return $"the door panel is at {Format(_door.Panel.localPosition)} instead of {Format(expected)} after opening";
            }

            _phase = Phase.Done;
            return null;
        }

        private bool HasLog(string prefix)
        {
            foreach (var log in _logs)
            {
                if (log.StartsWith(prefix, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private string Own(string rest)
        {
            return $"{CoopHoldLog.Tag} '{_interactable.name}' {rest}";
        }

        private string OfDoor(string rest)
        {
            return $"{CoopHoldLog.Tag} door '{_door.name}' {rest}";
        }

        private string Describe()
        {
            return $"holding={Count(_interactable.HoldingCount)} progress={_interactable.Progress.ToString("0.00", CultureInfo.InvariantCulture)}";
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        private static string Count(int count) => count.ToString(CultureInfo.InvariantCulture);

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }
    }
}
