using System;

public static class GameEvents
{
    public static Action<Character> OnTurnStarted;
    public static Action<Character> OnTurnEnded;
}
