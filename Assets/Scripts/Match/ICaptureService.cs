using System;
using System.Collections.Generic;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Consequence of a successful shot: jail a runner. The real cage/rescue system
    // will implement this later; Phase 4 ships a capture-only stub.
    public interface ICaptureService
    {
        void Capture(Character runner);
        event Action<Character> OnRunnerCaught;
        IReadOnlyList<CageStub> ActiveCages { get; }
    }
}
