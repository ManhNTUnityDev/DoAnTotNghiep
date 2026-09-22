using System.Collections.Generic;
using System.Linq;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Passive: the source of truth for Free/Jailed is Character.CaptureState.
    // The roster only stores membership and filters on demand; it raises no events.
    public class TeamRoster : ITeamRoster
    {
        private readonly List<Character> chasers = new List<Character>();
        private readonly List<Character> runners = new List<Character>();

        public IReadOnlyList<Character> Chasers => chasers;
        public IReadOnlyList<Character> Runners => runners;
        public Character PlayerCharacter { get; set; }

        public void Add(Character character)
        {
            if (character.Team == Team.Chaser)
            {
                chasers.Add(character);
            }
            else
            {
                runners.Add(character);
            }
        }

        public IReadOnlyList<Character> GetFreeRunners()
            => runners.Where(r => r.CaptureState == CaptureState.Free).ToList();

        public IReadOnlyList<Character> GetJailedRunners()
            => runners.Where(r => r.CaptureState == CaptureState.Jailed).ToList();

        public bool AllRunnersJailed()
            => runners.Count > 0 && runners.All(r => r.CaptureState == CaptureState.Jailed);
    }
}
