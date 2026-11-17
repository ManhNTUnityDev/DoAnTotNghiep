using System;
using System.Collections.Generic;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Consequence of a successful shot: jail a runner inside a cage for the rest of the
    // match. Implemented by JailService; there is no release path (rescue was dropped).
    public interface ICaptureService
    {
        void Capture(Character runner);
        event Action<Character> OnRunnerCaught;
        IReadOnlyList<Cage> ActiveCages { get; }
    }
}
