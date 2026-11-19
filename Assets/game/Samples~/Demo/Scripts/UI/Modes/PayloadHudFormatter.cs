using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class PayloadHudFormatter : ModeHudFormatterBase
    {
        public static LocalizedText Contested => new LocalizedText("mode.payload.contested");

        public static LocalizedText RollingBack => new LocalizedText("mode.payload.rollingBack");

        public static LocalizedText Idle => new LocalizedText("mode.payload.idle");

        public static LocalizedText YouAttack => new LocalizedText("mode.payload.youAttack");

        public static LocalizedText YouDefend => new LocalizedText("mode.payload.youDefend");

        // 表示が 100% になったのに試合が続いて見えないよう、切り上げずに切り捨てる。誤差で 29% が 28% にならないよう先に丸める。
        public static int ProgressPercent(float progress)
        {
            if (!(progress >= 0f && progress <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be between 0 and 1");
            }

            return (int)Math.Floor(Math.Round(progress * 100d, 4));
        }

        public static LocalizedText Progress(float progress)
        {
            return new LocalizedText("mode.payload.progress", ProgressPercent(progress));
        }

        public static LocalizedText Checkpoints(int reached, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            if (reached < 0 || reached > count)
            {
                throw new ArgumentOutOfRangeException(nameof(reached), reached, "reached must be between 0 and count");
            }

            return new LocalizedText("mode.payload.checkpoints", reached, count);
        }

        public static LocalizedText Pushing(int pushers)
        {
            if (pushers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(pushers), pushers, "pushing needs at least one pusher");
            }

            return new LocalizedText("mode.payload.pushing", pushers);
        }

        // 止められているときも攻め側はそばにいるので、押している人数より先に止められていることを伝える。
        public static LocalizedText Motion(PayloadStatus status)
        {
            Require(status);
            if (status.IsContested)
            {
                return Contested;
            }

            if (status.IsRollingBack)
            {
                return RollingBack;
            }

            // 人数が MinPushers に足りないと台車は進まないので、そばにいても押しているとは出さない。
            return status.Pushers >= status.MinPushers ? Pushing(status.Pushers) : Idle;
        }

        public static LocalizedText Role(Side side, Side attackers)
        {
            return new LocalizedText(side == attackers ? "mode.payload.attacking" : "mode.payload.defending", ModeHudText.SideName(side));
        }

        public static LocalizedText[] HudLines(PayloadStatus status, Side? ownSide)
        {
            Require(status);
            var lines = new List<LocalizedText> { Progress(status.Progress) };

            // 中継点の無い道筋で「0/0」と出しても何も伝わらないので、行ごと出さない。
            if (status.CheckpointCount > 0)
            {
                lines.Add(Checkpoints(status.CheckpointsReached, status.CheckpointCount));
            }

            lines.Add(Motion(status));
            if (ownSide.HasValue)
            {
                lines.Add(ownSide.Value == status.Attackers ? YouAttack : YouDefend);
            }

            return lines.ToArray();
        }

        // Payload の得点はいつも 0 なので、数の代わりに攻め・守りを出す。
        public override IReadOnlyList<ModeHudScoreLine> Scores(ModeStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }

            if (!(status.Detail is PayloadStatus payload))
            {
                return base.Scores(status);
            }

            var lines = new List<ModeHudScoreLine>(status.SideScores.Count);
            foreach (var sideScore in status.SideScores)
            {
                var side = sideScore.Side;
                var line = new ModeHudScoreLine(side, Role(side, payload.Attackers), side == status.OwnSide, false);
                if (line.IsOwn)
                {
                    lines.Insert(0, line);
                }
                else
                {
                    lines.Add(line);
                }
            }

            return lines;
        }

        protected override void AppendModeLines(ModeStatus status, List<LocalizedText> lines)
        {
            if (status.Detail is PayloadStatus payload)
            {
                lines.AddRange(HudLines(payload, status.OwnSide));
            }
        }

        private static void Require(PayloadStatus status)
        {
            if (status == null)
            {
                throw new ArgumentNullException(nameof(status));
            }
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
        // SubsystemRegistration は BeforeSceneLoad より先に走るので、ゲームが BeforeSceneLoad で登録した書式役がこれを置き換えられる。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            ModeHudFormatters.Register<PayloadStatus>(new PayloadHudFormatter());
        }
    }
}
