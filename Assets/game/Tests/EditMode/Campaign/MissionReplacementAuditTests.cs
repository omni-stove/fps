using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Shooter.Editor;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class MissionReplacementAuditTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void AnEmptyMissionDefinitionIsListedWithItsPlaceUntilItIsFilled()
        {
            var root = Track(new GameObject("Mission"));
            var child = new GameObject("Runner");
            child.transform.SetParent(root.transform);
            child.SetActive(false);
            var runner = child.AddComponent<MissionRunner>();
            var findings = new List<ReplacementAuditFinding>();

            ReplacementAudit.CheckGameObject(root, "Assets/Test.unity", findings);

            Assert.That(findings.Count(), Is.EqualTo(1));
            Assert.That(findings[0].Area, Is.EqualTo(ReplacementAuditArea.Reference));
            Assert.That(findings[0].Where, Is.EqualTo("Assets/Test.unity Mission/Runner"));
            Assert.That(findings[0].Problem, Does.StartWith("MissionRunner 'Definition' is empty"));

            runner.Definition = Track(ScriptableObject.CreateInstance<MissionDefinition>());
            findings.Clear();

            ReplacementAudit.CheckGameObject(root, "Assets/Test.unity", findings);

            Assert.That(findings, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
