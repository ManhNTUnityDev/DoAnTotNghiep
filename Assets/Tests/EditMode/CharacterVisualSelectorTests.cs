using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    public class CharacterVisualSelectorTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        private CharacterVisualSelector NewSelector(
            out GameObject firstPerson,
            out GameObject world,
            bool wireFirstPerson = true,
            bool wireWorld = true)
        {
            var root = new GameObject("Character");
            spawned.Add(root);

            firstPerson = null;
            world = null;

            if (wireFirstPerson)
            {
                firstPerson = new GameObject("Visual_FirstPerson");
                firstPerson.transform.SetParent(root.transform);
            }

            if (wireWorld)
            {
                world = new GameObject("Visual_World");
                world.transform.SetParent(root.transform);
            }

            var selector = root.AddComponent<CharacterVisualSelector>();
            selector.Configure(firstPerson, world);
            return selector;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            spawned.Clear();
        }

        // --- pure decision ---------------------------------------------------------------

        [Test]
        public void LocalPlayer_KeepsFirstPerson()
        {
            Assert.IsTrue(CharacterVisualSelector.ShouldKeepFirstPerson(true, true, true));
        }

        [Test]
        public void RemoteOrAi_KeepsWorldModel()
        {
            Assert.IsFalse(CharacterVisualSelector.ShouldKeepFirstPerson(false, true, true));
        }

        [Test]
        public void OnlyWorldWired_KeepsWorldEvenForLocalPlayer()
        {
            Assert.IsFalse(CharacterVisualSelector.ShouldKeepFirstPerson(true, false, true));
        }

        [Test]
        public void OnlyFirstPersonWired_KeepsItEvenForAi()
        {
            Assert.IsTrue(CharacterVisualSelector.ShouldKeepFirstPerson(false, true, false));
        }

        // --- Apply -----------------------------------------------------------------------

        [Test]
        public void Apply_ForLocalPlayer_DestroysWorldVisual()
        {
            var selector = NewSelector(out var firstPerson, out var world);

            selector.Apply(true);

            Assert.IsTrue(firstPerson != null, "first-person view model should survive");
            Assert.IsTrue(world == null, "world model should be destroyed");
        }

        [Test]
        public void Apply_ForAi_DestroysFirstPersonVisual()
        {
            var selector = NewSelector(out var firstPerson, out var world);

            selector.Apply(false);

            Assert.IsTrue(world != null, "world model should survive");
            Assert.IsTrue(firstPerson == null, "first-person view model should be destroyed");
        }

        // Two visuals both alive means two Animators ticking and two
        // CharacterAnimationControllers subscribed to OnAbilityUsed, firing every ability
        // animation twice. Exactly one must survive.
        [Test]
        public void Apply_LeavesExactlyOneVisualChild()
        {
            var selector = NewSelector(out _, out _);

            selector.Apply(true);

            Assert.AreEqual(1, selector.transform.childCount);
        }

        // SpawnManager is not the only path a Character is born through: CharacterFactory.Spawn is
        // the real choke point and Decoy.Use clones a whole character GameObject. A second Apply
        // must not destroy the survivor.
        [Test]
        public void Apply_CalledTwice_IsIdempotent()
        {
            var selector = NewSelector(out var firstPerson, out _);

            selector.Apply(true);
            selector.Apply(true);

            Assert.IsTrue(firstPerson != null, "survivor must not be destroyed by a second Apply");
            Assert.AreEqual(1, selector.transform.childCount);
        }

        [Test]
        public void Apply_CalledTwiceWithOppositeArgument_KeepsFirstDecision()
        {
            var selector = NewSelector(out var firstPerson, out var world);

            selector.Apply(true);
            selector.Apply(false);

            Assert.IsTrue(firstPerson != null, "the first decision wins");
            Assert.IsTrue(world == null);
            Assert.AreEqual(1, selector.transform.childCount);
        }

        [Test]
        public void HasApplied_IsFalseUntilApplyRuns()
        {
            var selector = NewSelector(out _, out _);

            Assert.IsFalse(selector.HasApplied, "nothing chosen yet; Start would fall back to world");

            selector.Apply(false);

            Assert.IsTrue(selector.HasApplied);
        }
    }
}
