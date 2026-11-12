using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Windrunner.Production.Core;
using Windrunner.Production.Input;
using Windrunner.Production.Feedback;
using Windrunner.Production.Animation;

namespace Windrunner.Production.Tests
{
    public sealed class ProductionRegressionTests
    {
        // The prototype is still in Assembly-CSharp. Reflection keeps the test
        // assembly independent without moving serialized scripts across assemblies.
        [TestCase("WindrunnerAirDashStateParityChecks", "Run")]
        [TestCase("WindrunnerAirCastReleaseParityChecks", "Run")]
        [TestCase("WindrunnerAirPowerStateChecks", "Run")]
        [TestCase("WindrunnerAirTargetSelectionParityChecks", "Run")]
        [TestCase("WindrunnerMotorStateChecks", "Run")]
        [TestCase("WindrunnerMotorSimulationChecks", "Run")]
        [TestCase("WindrunnerPlanarParityChecks", "Run")]
        [TestCase("WindrunnerSlingChargeParityChecks", "Run")]
        [TestCase("WindrunnerSlingDashParityChecks", "Run")]
        [TestCase("WindrunnerSlingFlowParityChecks", "Run")]
        [TestCase("WindrunnerSlingPathParityChecks", "Run")]
        [TestCase("WindrunnerSlingTargetParityChecks", "RunScoringChecks")]
        public void ExistingGameplayParity(string typeName, string method)
        {
            var type = Type.GetType("AirflowPrototype.Editor." + typeName + ", Assembly-CSharp-Editor", true);
            var entry = type.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
            Assert.That(entry, Is.Not.Null);
            var errors = new List<string>();
            bool reportedPass = false;
            Application.LogCallback capture = (message, trace, kind) =>
            {
                if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert)
                    errors.Add(message);
                if (kind == LogType.Log && message.Contains("PASS")) reportedPass = true;
            };
            Application.logMessageReceived += capture;
            try { entry.Invoke(null, null); }
            finally { Application.logMessageReceived -= capture; }
            Assert.That(errors, Is.Empty, "Existing checks must not swallow failures.");
            Assert.That(reportedPass, Is.True, "The parity tool must finish its comparisons.");
        }

        private sealed class State : IProjectState
        {
            private readonly List<string> events;
            private readonly string name;
            public State(List<string> events, string name) { this.events = events; this.name = name; }
            public void Enter() => events.Add(name + ".enter");
            public void Tick(float dt) => events.Add(name + ".tick:" + dt);
            public void Exit() => events.Add(name + ".exit");
        }

        [Test]
        public void StateTransitionsExitBeforeEnterAndDisposeOnce()
        {
            var events = new List<string>();
            var machine = new GameStateMachine();
            var first = new State(events, "first");
            machine.SetState(first);
            machine.SetState(first);
            machine.Tick(-1f);
            machine.SetState(new State(events, "second"));
            machine.Dispose();
            machine.Dispose();
            CollectionAssert.AreEqual(new[] { "first.enter", "first.tick:0", "first.exit", "second.enter", "second.exit" }, events);
            Assert.Throws<ObjectDisposedException>(() => machine.SetState(first));
        }

        [Test]
        public void PauseSwitchesMapsWithoutChangingAuthoringAsset()
        {
            var template = WindrunnerInputActionsFactory.CreateDefaults();
            var service = new WindrunnerInputService(template);
            try
            {
                Assert.That(template.FindActionMap("Gameplay").enabled, Is.False);
                Assert.That(service.GetAction("Gameplay", "Move").enabled, Is.True);
                service.SetMenuOpen(true);
                Assert.That(service.GetAction("Gameplay", "Move").enabled, Is.False);
                Assert.That(service.GetAction("System", "Pause").enabled, Is.True);
                Assert.That(service.Axis("Move"), Is.EqualTo(Vector2.zero));
                service.SetMenuOpen(false);
                Assert.That(service.GetAction("Gameplay", "Move").enabled, Is.True);
            }
            finally
            {
                // Dispose uses deferred Destroy, appropriate in Play Mode; remove
                // the runtime clone explicitly here before disposing in Edit Mode.
                var clone = service.Actions;
                UnityEngine.Object.DestroyImmediate(clone);
                service.Dispose();
                UnityEngine.Object.DestroyImmediate(template);
            }
        }

        [Test]
        public void FeedbackObservationPreservesPublishOrderAndDisposalStopsDelivery()
        {
            var service = new FeedbackService(null, null, null, null, null, false);
            var events = new List<FeedbackKey>();
            service.Observed += (key, request) => events.Add(key);
            service.Publish(FeedbackKey.SlingImpact, default);
            service.Publish(FeedbackKey.SlingLaunched, default);
            service.Publish(FeedbackKey.SlingPerfect, default);
            service.Dispose();
            service.Publish(FeedbackKey.CastReleased, default);
            CollectionAssert.AreEqual(new[] { FeedbackKey.SlingImpact, FeedbackKey.SlingLaunched, FeedbackKey.SlingPerfect }, events);
            Assert.That(service.PlayedCount, Is.Zero);
        }

        [Test]
        public void AnimationObservationUnbindsPreviousServiceAndDisposes()
        {
            var first = new FeedbackService(null, null, null, null, null, false);
            var second = new FeedbackService(null, null, null, null, null, false);
            var session = new WindrunnerAnimationSession(null, null, null);
            try
            {
                session.BindFeedback(first);
                first.Publish(FeedbackKey.Jump, default);
                session.BindFeedback(second);
                first.Publish(FeedbackKey.Land, default);
                second.Publish(FeedbackKey.SlingImpact, default);
                Assert.That(session.ObservedEvents, Is.EqualTo(2));
                Assert.That(session.PortableActive, Is.False);
                session.Dispose();
                second.Publish(FeedbackKey.SlingLaunched, default);
                Assert.That(session.ObservedEvents, Is.EqualTo(2));
            }
            finally { session.Dispose(); first.Dispose(); second.Dispose(); }
        }
    }
}
