namespace ChaseGame.Match
{
    // Pure HUD clock rendering. Clamps negative time to 0 so the display never shows -0:01.
    public static class MatchClock
    {
        public static string Format(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int total = (int)seconds;         // floor toward zero for non-negative input
            int minutes = total / 60;
            int secs = total % 60;
            return minutes + ":" + secs.ToString("00");
        }
    }
}
