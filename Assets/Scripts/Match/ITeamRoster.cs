using System.Collections.Generic;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    public interface ITeamRoster
    {
        IReadOnlyList<Character> Chasers { get; }
        IReadOnlyList<Character> Runners { get; }
        Character PlayerCharacter { get; set; }

        void Add(Character character);

        IReadOnlyList<Character> GetFreeRunners();
        IReadOnlyList<Character> GetJailedRunners();
        bool AllRunnersJailed();
    }
}
