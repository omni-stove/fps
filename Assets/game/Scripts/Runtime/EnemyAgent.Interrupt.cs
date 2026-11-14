using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed partial class EnemyAgent
    {
        private readonly EnemyInterruption _interruption = new EnemyInterruption();

        public bool IsInterrupted => _interruption.IsActive;

        public string InterruptReason => _interruption.IsActive ? _interruption.Reason : string.Empty;

        public float InterruptSecondsRemaining => _interruption.SecondsRemaining;

        // 止めている間も知覚に渡す撃たれた記録は捨てない。怯みが解けた直後に、撃たれた方へ隠れたり向かったりできるように。
        public bool Interrupt(float seconds, string reason)
        {
            EnemyInterruption.Validate(seconds, reason);
            if (_brain == null)
            {
                throw new InvalidOperationException($"EnemyAgent '{name}' is not initialized");
            }

            if (_brain.IsDead || !_interruption.Begin(seconds, reason))
            {
                return false;
            }

            Debug.Log(FormatInterruptLog(Id, reason, seconds), this);
            return true;
        }

        public static string FormatInterruptLog(string id, string reason, float seconds)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} id={1} interrupted reason={2} seconds={3:F2}", LogTag, id, reason, seconds);
        }

        public static string FormatResumeLog(string id, string reason)
        {
            return $"{LogTag} id={id} resumed reason={reason}";
        }

        // 止まっている間は判断を進めない。進めると見失いや捜索の時間だけが経ち、解けたときに状態が飛ぶ。
        private bool HoldWhileInterrupted(float deltaSeconds)
        {
            if (!_interruption.IsActive)
            {
                return false;
            }

            if (_interruption.Tick(deltaSeconds))
            {
                Debug.Log(FormatResumeLog(Id, _interruption.Reason), this);
                _interruption.Clear();
                return false;
            }

            _sprinting = false;
            _aiming = false;
            _agent.speed = _walkSpeed;
            MoveTo(null);
            WatchProgress(deltaSeconds);
            return true;
        }
    }
}
