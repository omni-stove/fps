using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// 射撃場のチュートリアルの進み。手順 (<see cref="Order"/>: 移動・照準・射撃・リロード・しゃがみ・カバー) を順に 1 つずつ進め、
    /// 今の手順を終えたかを毎フレームの観測 (<see cref="Observe"/>) から決める。
    /// 手順ごとの数え (歩いた距離・覗き込んだ時間・当てた回数) はその手順に入ってから数え始め、前の手順でしたことは持ち越さない。
    /// 画面に出す文 (<see cref="Prompt"/>) も決める。使うキーの表記は呼ぶ側が今の入力機器と割り当てから渡す。
    /// </summary>
    public sealed class TutorialSteps
    {
        /// <summary>移動の手順を終えるまでに歩く距離 (m)。</summary>
        public const float DefaultMoveMeters = 3f;

        /// <summary>照準の手順を終えるまでに覗き込む時間 (秒、途中で離してもよい)。</summary>
        public const float DefaultAimSeconds = 0.5f;

        /// <summary>射撃の手順を終えるまでに標的に当てる回数。</summary>
        public const int DefaultHitsRequired = 3;

        /// <summary>
        /// 1 回の観測でこれより長く動いたら、歩いたのではなく位置を移された (出現し直した) と見なして数えない (m)。
        /// ダッシュ (8m/s) で 10fps まで落ちても 0.8m なので、歩きを捨てることはない。
        /// </summary>
        public const float DefaultTeleportMeters = 2f;

        /// <summary>すべて終えたときに出す文。</summary>
        public const string CompletedText = "チュートリアル完了";

        /// <summary>手順の並び。この順に進む。</summary>
        public static IReadOnlyList<TutorialStep> Order { get; } = new[]
        {
            TutorialStep.Move,
            TutorialStep.Aim,
            TutorialStep.Shoot,
            TutorialStep.Reload,
            TutorialStep.Crouch,
            TutorialStep.Cover,
        };

        private readonly float _moveMeters;
        private readonly float _aimSeconds;
        private readonly int _hitsRequired;
        private readonly float _teleportMeters;

        private int _index;
        private float _movedMeters;
        private float _aimedSeconds;
        private int _hits;

        /// <param name="moveMeters">移動の手順で歩く距離 (m)。0 より大きい有限値。</param>
        /// <param name="aimSeconds">照準の手順で覗き込む時間 (秒)。0 以上の有限値 (0 なら覗き込んだ時点で終える)。</param>
        /// <param name="hitsRequired">射撃の手順で当てる回数。1 以上。</param>
        /// <param name="teleportMeters">1 回の観測でこれより長い移動は数えない (m)。moveMeters 以下でもよいが 0 より大きい有限値。</param>
        public TutorialSteps(
            float moveMeters = DefaultMoveMeters,
            float aimSeconds = DefaultAimSeconds,
            int hitsRequired = DefaultHitsRequired,
            float teleportMeters = DefaultTeleportMeters)
        {
            if (!(moveMeters > 0f) || float.IsInfinity(moveMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(moveMeters), moveMeters, "moveMeters must be a positive finite number");
            }

            if (!(aimSeconds >= 0f) || float.IsInfinity(aimSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(aimSeconds), aimSeconds, "aimSeconds must be a non-negative finite number");
            }

            if (hitsRequired < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hitsRequired), hitsRequired, "hitsRequired must be at least 1");
            }

            if (!(teleportMeters > 0f) || float.IsInfinity(teleportMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(teleportMeters), teleportMeters, "teleportMeters must be a positive finite number");
            }

            _moveMeters = moveMeters;
            _aimSeconds = aimSeconds;
            _hitsRequired = hitsRequired;
            _teleportMeters = teleportMeters;
        }

        public float MoveMeters => _moveMeters;

        public float AimSeconds => _aimSeconds;

        public int HitsRequired => _hitsRequired;

        /// <summary>すべての手順を終えたか。</summary>
        public bool IsComplete => _index >= Order.Count;

        /// <summary>今の手順の位置 (0 始まり)。すべて終えたら <see cref="Order"/> の数。</summary>
        public int CurrentIndex => _index;

        /// <summary>今の手順。</summary>
        /// <exception cref="InvalidOperationException">すべて終えている。</exception>
        public TutorialStep Current
        {
            get
            {
                if (IsComplete)
                {
                    throw new InvalidOperationException("the tutorial is complete; there is no current step");
                }

                return Order[_index];
            }
        }

        /// <summary>射撃の手順で、ここまでに当てた回数。他の手順では 0。</summary>
        public int Hits => _hits;

        /// <summary>
        /// 今の手順の進み (0〜1)。移動は歩いた距離、照準は覗き込んだ時間、射撃は当てた回数で、他の手順は終えるまで 0。
        /// すべて終えたら 1。
        /// </summary>
        public float StepProgress
        {
            get
            {
                if (IsComplete)
                {
                    return 1f;
                }

                switch (Current)
                {
                    case TutorialStep.Move:
                        return Math.Min(1f, _movedMeters / _moveMeters);
                    case TutorialStep.Aim:
                        return _aimSeconds > 0f ? Math.Min(1f, _aimedSeconds / _aimSeconds) : 0f;
                    case TutorialStep.Shoot:
                        return Math.Min(1f, (float)_hits / _hitsRequired);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// メニューのチュートリアルから来たか (requestedFromMenu)、まだ一度も終えていなければ (alreadyCompleted が false) 始める。
        /// </summary>
        public static bool ShouldStart(bool alreadyCompleted, bool requestedFromMenu)
        {
            return requestedFromMenu || !alreadyCompleted;
        }

        /// <summary>
        /// 1 フレーム分の様子 observation で今の手順を数え、終えたら次の手順へ進めて true を返す。
        /// 1 回で進むのは 1 手順だけ (同じフレームで次の手順の条件を満たしていても、それは次の観測で数える)。
        /// すべて終えていれば何もせず false。
        /// </summary>
        public bool Observe(TutorialObservation observation)
        {
            if (IsComplete)
            {
                return false;
            }

            if (!IsStepDone(observation))
            {
                return false;
            }

            _index++;
            ResetCounts();
            return true;
        }

        /// <summary>最初の手順からやり直す。</summary>
        public void Restart()
        {
            _index = 0;
            ResetCounts();
        }

        /// <summary>
        /// 画面に出す文。今の手順を keyLabel のキーで案内し、頭に「[今の番号/手順の数]」を付ける (例: <c>[1/6] WASD で移動</c>)。
        /// 射撃の手順では当てた回数も付ける。すべて終えたら <see cref="CompletedText"/>。
        /// </summary>
        /// <param name="keyLabel">今の手順で使うキー (またはボタン) の表記。空にはできない。すべて終えていれば使わない。</param>
        public string Prompt(string keyLabel)
        {
            if (IsComplete)
            {
                return CompletedText;
            }

            var body = Instruction(Current, keyLabel, _hitsRequired);
            if (Current == TutorialStep.Shoot)
            {
                body += string.Format(CultureInfo.InvariantCulture, " ({0}/{1})", _hits, _hitsRequired);
            }

            return string.Format(CultureInfo.InvariantCulture, "[{0}/{1}] {2}", _index + 1, Order.Count, body);
        }

        /// <summary>step を keyLabel のキーで行うよう案内する文 (番号や進みは付けない)。</summary>
        /// <param name="step">案内する手順。</param>
        /// <param name="keyLabel">操作に使うキーの表記。空にはできない。</param>
        /// <param name="hitsRequired">射撃の手順で当てる回数。1 以上。</param>
        public static string Instruction(TutorialStep step, string keyLabel, int hitsRequired = DefaultHitsRequired)
        {
            if (string.IsNullOrWhiteSpace(keyLabel))
            {
                throw new ArgumentException("keyLabel must not be empty", nameof(keyLabel));
            }

            if (hitsRequired < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hitsRequired), hitsRequired, "hitsRequired must be at least 1");
            }

            var key = keyLabel.Trim();
            switch (step)
            {
                case TutorialStep.Move:
                    return key + " で移動";
                case TutorialStep.Aim:
                    return key + " で照準を覗く";
                case TutorialStep.Shoot:
                    return string.Format(CultureInfo.InvariantCulture, "{0} で撃って標的に {1} 回当てる", key, hitsRequired);
                case TutorialStep.Reload:
                    return key + " でリロード";
                case TutorialStep.Crouch:
                    return key + " でしゃがむ";
                case TutorialStep.Cover:
                    return "遮蔽物に近づいて " + key + " でカバー";
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "unknown tutorial step");
            }
        }

        /// <summary>
        /// 方向ごとに分かれたキー (上・左・下・右の順) を 1 つの表記にまとめる。どれも 1 文字ならつなげ (<c>WASD</c>)、
        /// そうでなければ / で区切る (<c>Up/Left/Down/Right</c>)。空の要素は飛ばす。
        /// </summary>
        /// <returns>まとめた表記。空でない要素が無ければ空文字。</returns>
        public static string JoinDirectionalKeys(IReadOnlyList<string> keys)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            var parts = new List<string>(keys.Count);
            var allSingle = true;
            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var trimmed = key.Trim();
                parts.Add(trimmed);
                allSingle &= trimmed.Length == 1;
            }

            return string.Join(allSingle ? string.Empty : "/", parts);
        }

        private bool IsStepDone(TutorialObservation observation)
        {
            switch (Current)
            {
                case TutorialStep.Move:
                    // 位置を移されたときの跳びは歩いた距離に入れない
                    if (observation.MovedMeters <= _teleportMeters)
                    {
                        _movedMeters += observation.MovedMeters;
                    }

                    return _movedMeters >= _moveMeters;
                case TutorialStep.Aim:
                    if (!observation.Aiming)
                    {
                        return false;
                    }

                    _aimedSeconds += observation.DeltaSeconds;
                    return _aimedSeconds >= _aimSeconds;
                case TutorialStep.Shoot:
                    _hits = (int)Math.Min((long)_hits + observation.Hits, _hitsRequired);
                    return _hits >= _hitsRequired;
                case TutorialStep.Reload:
                    return observation.Reloading;
                case TutorialStep.Crouch:
                    return observation.Crouching;
                case TutorialStep.Cover:
                    return observation.InCover;
                default:
                    throw new InvalidOperationException("unknown tutorial step " + Current);
            }
        }

        private void ResetCounts()
        {
            _movedMeters = 0f;
            _aimedSeconds = 0f;
            _hits = 0;
        }
    }
}
