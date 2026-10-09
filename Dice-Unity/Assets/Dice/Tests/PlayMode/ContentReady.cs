using System.Collections;
using Dice.App;
using NUnit.Framework;
using UnityEngine;

namespace Dice.Tests
{
    internal static class ContentReady
    {
        internal static IEnumerator Spirit()
        {
            var controller = Object.FindAnyObjectByType<SpiritEncounterController>();
            Assert.That(controller, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.Snapshot == null && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(controller.StatusMessage, Does.Not.StartWith("Cannot"));
                yield return null;
            }
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
        }

        internal static IEnumerator Diagnostic()
        {
            var controller = Object.FindAnyObjectByType<DiagnosticEncounterController>();
            Assert.That(controller, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 30;
            while (controller.Snapshot == null && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(controller.StatusMessage, Does.Not.StartWith("Cannot"));
                yield return null;
            }
            Assert.That(controller.Snapshot, Is.Not.Null, controller.StatusMessage);
        }
    }
}
