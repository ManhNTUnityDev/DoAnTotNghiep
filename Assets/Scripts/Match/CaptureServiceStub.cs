using System;
using System.Collections.Generic;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // Capture-only stub: jails a free runner (the Body stops via CaptureState),
    // records a CageStub, and raises OnRunnerCaught. No rescue/zone yet.
    public sealed class CaptureServiceStub : ICaptureService
    {
        private readonly List<CageStub> cages = new List<CageStub>();

        public IReadOnlyList<CageStub> ActiveCages => cages;
        public event Action<Character> OnRunnerCaught;

        public void Capture(Character runner)
        {
            if (runner == null || runner.Team != Team.Runner || runner.CaptureState == CaptureState.Jailed)
            {
                return;
            }

            runner.CaptureState = CaptureState.Jailed; // Body Stop()s via the setter
            cages.Add(new CageStub { Occupant = runner, Position = runner.transform.position });
            OnRunnerCaught?.Invoke(runner);
        }
    }
}
