using System;

public static class GameEvents
{
    public static Action<Character> OnTurnStarted;
    public static Action<Character> OnTurnEnded;
    public static Action<Character> OnCharacterDeathCompleted;
    public static Action<Character, int> OnEnergyRecovered;
}
