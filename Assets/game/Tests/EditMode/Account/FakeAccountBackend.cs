using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    // 呼び戻しは Hold の間は手元に溜め、Release で返す。Hold でなければ呼んだその場で返す。
    internal sealed class FakeAccountBackend : IAccountBackend
    {
        private readonly List<Action> _held = new List<Action>();

        public List<string> Calls { get; } = new List<string>();

        public bool Available { get; set; } = true;

        public bool EpicAccount { get; set; } = true;

        public string UnavailableReason { get; set; } = "no EOS credentials";

        public AccountKind SignedInKind { get; set; }

        public AccountLoginResult DeviceResult { get; set; } = AccountLoginResult.SignedIn();

        public AccountLoginResult RestoreResult { get; set; } = AccountLoginResult.NoSavedLogin("not found");

        public AccountLoginResult EpicResult { get; set; } = AccountLoginResult.SignedIn();

        public string SignOutError { get; set; }

        public bool Hold { get; set; }

        public int HeldCount => _held.Count;

        public int Prepares { get; private set; }

        public int Ticks { get; private set; }

        public bool TryPrepare(out bool epicAccount, out string unavailable)
        {
            Prepares++;
            Calls.Add("prepare");
            epicAccount = Available && EpicAccount;
            unavailable = Available ? null : UnavailableReason;
            return Available;
        }

        public void Tick()
        {
            Ticks++;
        }

        public void SignInDevice(Action<AccountLoginResult> done)
        {
            Calls.Add("device");
            Run(() =>
            {
                if (DeviceResult.Succeeded)
                {
                    SignedInKind = AccountKind.Device;
                }

                done(DeviceResult);
            });
        }

        public void RestoreEpic(Action<AccountLoginResult> done)
        {
            Calls.Add("restore");
            Run(() =>
            {
                if (RestoreResult.Succeeded)
                {
                    SignedInKind = AccountKind.Epic;
                }

                done(RestoreResult);
            });
        }

        public void SignInEpic(Action<AccountLoginResult> done)
        {
            Calls.Add("epic");
            Run(() =>
            {
                if (EpicResult.Succeeded)
                {
                    SignedInKind = AccountKind.Epic;
                }

                done(EpicResult);
            });
        }

        public void SignOutEpic(Action<string> done)
        {
            Calls.Add("sign-out");
            Run(() =>
            {
                if (SignedInKind == AccountKind.Epic)
                {
                    SignedInKind = AccountKind.None;
                }

                done(SignOutError);
            });
        }

        public void Release()
        {
            var held = new List<Action>(_held);
            _held.Clear();
            foreach (var action in held)
            {
                action();
            }
        }

        private void Run(Action action)
        {
            if (Hold)
            {
                _held.Add(action);
                return;
            }

            action();
        }
    }
}
