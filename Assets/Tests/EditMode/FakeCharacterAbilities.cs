using System.Collections.Generic;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.Tests
{
    // Records ability usage so brain trees can be tested without real abilities.
    public class FakeCharacterAbilities : ICharacterAbilities
    {
        public readonly List<int> Used = new List<int>();
        public readonly HashSet<int> ReadyIds = new HashSet<int> { 0, 1 };

        public bool IsReady(int id) => ReadyIds.Contains(id);

        public void UseAbility(int id, Vector3 aim) => Used.Add(id);
    }
}
