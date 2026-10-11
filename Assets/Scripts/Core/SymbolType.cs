namespace SlotGame.Core
{
    /// <summary>
    /// The four classic slot symbols available in this game.
    /// Ordered from highest to lowest payout value.
    /// </summary>
    public enum SymbolType
    {
        Seven = 0,  // Jackpot symbol (highest value)
        Bar   = 1,
        Bell  = 2,
        Cherry = 3  // Most common symbol (lowest value)
    }
}
