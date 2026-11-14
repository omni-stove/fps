using System;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場の smoke (<see cref="TpsRangeSmokeRun"/>) に足す、崖のぼりの手順。段は <see cref="MantleLedgesFixture"/>。
    /// <list type="number">
    /// <item>射撃場のルールで崖のぼり (<see cref="ActionRules.Mantle"/>) が On になっている。</item>
    /// <item>薄い段の前で <see cref="PlayerController.TryStartMantle"/> しても登らず、<c>[Player] mantle=TooNarrow</c> が出る。</item>
    /// <item>天井のある段の前でも登らず、<c>[Player] mantle=Blocked</c> が出る。</item>
    /// <item>登れる段の前で、前へ進みながら接地しているフレームに Jump を押すと (乗り越えと同じ入力)、
    /// <c>[Player] mantle=Mantle</c> → <c>[Player] mantling=true</c> → <c>mantling=false</c> の順に出て、段の上に立つ。
    /// 登っている間は <see cref="PlayerActionReader"/> の状態に <see cref="PlayerActionConditions.Mantling"/> が立つ。ふつうのジャンプになったら失敗にする。</item>
    /// </list>
    /// 低い壁を今までどおり乗り越えることは、組み込みの乗り越えの手順が崖のぼりを On にしたルールで確かめる。
    /// </summary>
    public sealed class MantleSmokeStep : ITpsActionSmokeStep
    {
        /// <summary>段の前に置く位置の、体の表面から段の手前の面までの隙間 (m)。体の中心から面までが <see cref="VaultProbe.MaxReach"/> に収まる。</summary>
        public const float StandOff = 0.3f;

        /// <summary>段の前に移してから調べ始めるまでに待つ、Editor の更新の回数。移した位置で接地し直すのを待つ。</summary>
        public const int SettleUpdates = 3;

        /// <summary>登り終えた足元の高さの、段の上端からの許容のずれ (m)。CharacterController の skinWidth を見込む。</summary>
        public const float FeetTolerance = 0.15f;

        private static readonly string _tooNarrowLog = ProbeLogPrefix(VaultVerdict.TooNarrow);
        private static readonly string _blockedLog = ProbeLogPrefix(VaultVerdict.Blocked);
        private static readonly string _mantleLog = ProbeLogPrefix(VaultVerdict.Mantle);
        private static readonly string _startLog = PlayerController.FormatMantlingLog(true);
        private static readonly string _endLog = PlayerController.FormatMantlingLog(false);

        private Phase _phase;
        private int _settled;
        private bool _sawTooNarrow;
        private bool _sawBlocked;
        private bool _sawMantle;
        private bool _sawStart;
        private bool _sawEnd;
        private bool _started;
        private bool _flagWhileMantling;
        private PlayerController _mover;
        private ClimbFilter _filter;

        private enum Phase
        {
            CheckRules,
            PlaceAtNarrow,
            ProbeNarrow,
            PlaceAtCovered,
            ProbeCovered,
            PlaceAtHigh,
            Climb,
            Done,
        }

        public int Order => 100;

        public string Name => "mantle";

        public float TimeoutSeconds => 20f;

        public void Reset()
        {
            Cleanup();
            _phase = Phase.CheckRules;
            _settled = 0;
            _sawTooNarrow = false;
            _sawBlocked = false;
            _sawMantle = false;
            _sawStart = false;
            _sawEnd = false;
            _started = false;
            _flagWhileMantling = false;
        }

        public void OnLog(string condition)
        {
            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(_tooNarrowLog, StringComparison.Ordinal))
            {
                _sawTooNarrow = true;
            }
            else if (condition.StartsWith(_blockedLog, StringComparison.Ordinal))
            {
                _sawBlocked = true;
            }
            else if (condition.StartsWith(_mantleLog, StringComparison.Ordinal))
            {
                _sawMantle = true;
            }
            else if (string.Equals(condition, _startLog, StringComparison.Ordinal))
            {
                _sawStart = true;
            }
            else if (string.Equals(condition, _endLog, StringComparison.Ordinal))
            {
                _sawEnd = true;
            }
        }

        public bool Advance(BootstrapWorld world, out string problem)
        {
            problem = null;
            var mover = world.Mover;
            switch (_phase)
            {
                case Phase.CheckRules:
                    if (world.Rules.Actions.Mantle != Mantle.On || mover.MantleRule != Mantle.On)
                    {
                        problem = $"the TPS range rules must turn mantle on; rerun {GameRulesAssetBuilder.MenuPath} (rules {world.Rules.Actions.Mantle}, player {mover.MantleRule})";
                        return false;
                    }

                    _phase = Phase.PlaceAtNarrow;
                    return false;

                case Phase.PlaceAtNarrow:
                    problem = PlaceAt(world, MantleLedgesFixture.NarrowLedgeName);
                    _phase = Phase.ProbeNarrow;
                    return false;

                case Phase.ProbeNarrow:
                    problem = ExpectRefused(mover, MantleLedgesFixture.NarrowLedgeName, VaultVerdict.TooNarrow, () => _sawTooNarrow, Phase.PlaceAtCovered);
                    return false;

                case Phase.PlaceAtCovered:
                    problem = PlaceAt(world, MantleLedgesFixture.CoveredLedgeName);
                    _phase = Phase.ProbeCovered;
                    return false;

                case Phase.ProbeCovered:
                    problem = ExpectRefused(mover, MantleLedgesFixture.CoveredLedgeName, VaultVerdict.Blocked, () => _sawBlocked, Phase.PlaceAtHigh);
                    return false;

                case Phase.PlaceAtHigh:
                    problem = PlaceAt(world, MantleLedgesFixture.HighLedgeName);
                    if (problem == null)
                    {
                        _mover = mover;
                        _filter = new ClimbFilter(mover.MoveFilter, this);
                        mover.MoveFilter = _filter;
                        mover.MantleStarted += OnMantleStarted;
                        _phase = Phase.Climb;
                    }

                    return false;

                case Phase.Climb:
                    return Climb(world, out problem);

                default:
                    return true;
            }
        }

        public void Cleanup()
        {
            if (_mover != null)
            {
                _mover.MantleStarted -= OnMantleStarted;
                if (_filter != null && ReferenceEquals(_mover.MoveFilter, _filter))
                {
                    _mover.MoveFilter = _filter.Inner;
                }
            }

            _mover = null;
            _filter = null;
        }

        private static string ProbeLogPrefix(VaultVerdict verdict)
        {
            return $"{PlayerController.LogTag} mantle={verdict} ";
        }

        private void OnMantleStarted(Vector3 direction)
        {
            _started = true;
        }

        private string PlaceAt(BootstrapWorld world, string ledgeName)
        {
            var ledge = FindLedge(ledgeName, out var problem);
            if (ledge == null)
            {
                return problem;
            }

            var bounds = ledge.bounds;
            var standOff = GameBootstrap.PlayerRadius + StandOff;
            Teleport(world.Controller, new Vector3(bounds.center.x, GameBootstrap.PlayerSpawn.y, bounds.min.z - standOff));
            FaceForward(world);
            _settled = 0;
            return null;
        }

        private string ExpectRefused(PlayerController mover, string ledgeName, VaultVerdict expected, Func<bool> sawLog, Phase next)
        {
            if (!mover.IsGrounded || ++_settled < SettleUpdates)
            {
                return null;
            }

            if (mover.TryStartMantle())
            {
                return $"the player climbed '{ledgeName}', which must be refused as {expected}";
            }

            var verdict = mover.LastMantleProbe.Verdict;
            if (verdict != expected)
            {
                return $"'{ledgeName}' was judged {PlayerController.FormatMantleProbeLog(mover.LastMantleProbe)} (expected {expected})";
            }

            if (!sawLog())
            {
                return $"refusing '{ledgeName}' logged no '{ProbeLogPrefix(expected)}...' line";
            }

            _phase = next;
            return null;
        }

        private bool Climb(BootstrapWorld world, out string problem)
        {
            problem = null;
            var mover = world.Mover;
            if (_filter == null || !ReferenceEquals(mover.MoveFilter, _filter))
            {
                problem = "something replaced the move filter while climbing the ledge";
                return false;
            }

            // Jump は接地しているフレームにだけ押すので、押したのによじ登り始めていなければ、そのフレームはふつうのジャンプになっている。
            if (_filter.JumpedWhileGrounded && !_started)
            {
                problem = $"jumping in front of '{MantleLedgesFixture.HighLedgeName}' did not start a climb ({PlayerController.FormatMantleProbeLog(mover.LastMantleProbe)}, {PlayerController.FormatVaultProbeLog(mover.LastVaultProbe)})";
                return false;
            }

            if (!_started)
            {
                return false;
            }

            if (mover.IsMantling)
            {
                var reader = world.ActionReader;
                if (reader != null && reader.Current.IsMantling)
                {
                    _flagWhileMantling = true;
                }

                return false;
            }

            if (!mover.IsGrounded)
            {
                return false;
            }

            if (!_sawMantle || !_sawStart || !_sawEnd)
            {
                problem = $"the climb did not log '{_mantleLog}...', '{_startLog}' and '{_endLog}' (probe={_sawMantle} start={_sawStart} end={_sawEnd})";
                return false;
            }

            if (!_flagWhileMantling)
            {
                problem = $"the action state never had {PlayerActionConditions.Mantling} while climbing";
                return false;
            }

            var feetY = world.Controller.bounds.min.y;
            var bodyZ = world.Player.transform.position.z;
            if (Mathf.Abs(feetY - MantleLedgesFixture.LedgeHeight) > FeetTolerance || bodyZ - GameBootstrap.PlayerRadius < MantleLedgesFixture.FrontZ)
            {
                problem = string.Format(
                    CultureInfo.InvariantCulture,
                    "after the climb the feet are at y={0:0.##} and the body at z={1:0.##}, not standing on top of '{2}' (top {3:0.##}, front z {4:0.##})",
                    feetY,
                    bodyZ,
                    MantleLedgesFixture.HighLedgeName,
                    MantleLedgesFixture.LedgeHeight,
                    MantleLedgesFixture.FrontZ);
                return false;
            }

            Cleanup();
            _phase = Phase.Done;
            return true;
        }

        private static Collider FindLedge(string name, out string problem)
        {
            var found = GameObject.Find(MantleLedgesFixture.RootName + "/" + name);
            var collider = found != null ? found.GetComponent<Collider>() : null;
            problem = collider == null ? $"the scene has no '{MantleLedgesFixture.RootName}/{name}' with a collider; rerun {TpsRangeSceneBuilder.MenuPath}" : null;
            return collider;
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

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceForward(BootstrapWorld world)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 入力の代わりに、よじ登り始めるまでは前進を押し続けて接地しているフレームにだけ Jump を押し、始めたら何も押さない差し込み。
        /// 登った後も前へ進むと奥の縁から落ちるので止める。先に差し込まれていたもの (カバー) は後から呼び、カバーの制限を上書きしない。
        /// </summary>
        private sealed class ClimbFilter : IPlayerMoveFilter
        {
            private readonly MantleSmokeStep _step;

            public ClimbFilter(IPlayerMoveFilter inner, MantleSmokeStep step)
            {
                Inner = inner;
                _step = step;
            }

            public IPlayerMoveFilter Inner { get; }

            public bool JumpedWhileGrounded { get; private set; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                var climbing = _step._started;
                move = climbing ? Vector2.zero : Vector2.up;
                sprint = false;
                crouch = false;
                jumpPressed = !climbing && _step._mover != null && _step._mover.IsGrounded;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
                if (jumpPressed)
                {
                    JumpedWhileGrounded = true;
                }
            }
        }
    }
}
