using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // ネット対戦の相手は部品 (SideMember) を持たないので、キットが GameSignals.Nameplates に出す集め方から受け、名前は同じ画面が出す名簿 (GameSignals.Participants) で引く。
    public sealed class SignalNameplateTargets : INameplateTargets
    {
        private readonly List<NameplateContact> _contacts = new List<NameplateContact>();
        private readonly IParticipants _participants;

        public SignalNameplateTargets(INameplateContacts contacts, IParticipants participants = null)
        {
            Contacts = contacts ?? throw new ArgumentNullException(nameof(contacts));
            _participants = participants;
        }

        public INameplateContacts Contacts { get; }

        // 名簿は地図を替えるたびに HUD ごと作り直されるので、渡されていなければ毎回今の値を引く。
        public IParticipants Participants => _participants ?? GameSignals.Participants.Value;

        public void CopyTo(Vector3 eye, List<NameplateTarget> targets)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            _contacts.Clear();
            Contacts.CopyTo(eye.ToNumerics(), _contacts);
            foreach (var contact in _contacts)
            {
                targets.Add(new NameplateTarget(
                    contact.Id, contact.HeadPosition.ToUnity(), contact.Relation, contact.IsAlive, contact.IsInSight));
            }

            _contacts.Clear();
        }
    }
}
