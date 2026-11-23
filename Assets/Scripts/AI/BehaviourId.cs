namespace ChaseGame.AI
{
    // The behaviour an NPC is currently carrying out. Set by the shared action functions,
    // so a Behaviour Tree brain and an FSM brain report it identically.
    public enum BehaviourId
    {
        None,
        Wander,
        Chase,
        Shoot,
        Flee,
        Search,
        Hide,
    }
}
