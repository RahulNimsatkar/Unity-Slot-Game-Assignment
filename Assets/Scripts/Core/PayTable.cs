namespace SlotGame.Core
{
    /// <summary>Categories of win, ordered by excitement level.</summary>
    public enum WinKind
    {
        None = 0,
        Pair = 1,          // Bonus: exactly two cherries (classic slot rule)
        ThreeOfAKind = 2,  // All three reels match (core assignment requirement)
        Jackpot = 3        // Three Sevens - the top prize
    }

    /// <summary>Result of evaluating one spin against the pay table.</summary>
    public readonly struct WinResult
    {
        public readonly WinKind Kind;
        public readonly SymbolType Symbol;   // Symbol that formed the win (undefined when Kind == None)
        public readonly int Multiplier;      // Bet multiplier awarded
        public readonly string Label;        // Human readable description for the UI

        public WinResult(WinKind kind, SymbolType symbol, int multiplier, string label)
        {
            Kind = kind;
            Symbol = symbol;
            Multiplier = multiplier;
            Label = label;
        }

        public static readonly WinResult NoWin =
            new WinResult(WinKind.None, SymbolType.Cherry, 0, "NO WIN");
    }

    /// <summary>
    /// Static pay table. Contains every winning rule of the game in one place so
    /// balancing only ever happens here.
    ///
    /// Rules:
    ///  - 7  7  7   -> x50  (JACKPOT)
    ///  - BAR BAR BAR -> x20
    ///  - Bell x3   -> x10
    ///  - Cherry x3 -> x5
    ///  - Exactly two Cherries anywhere -> x2 (bonus rule)
    ///  - Anything else -> 0
    /// </summary>
    public static class PayTable
    {
        // Three-of-a-kind multipliers, indexed by (int)SymbolType.
        private static readonly int[] ThreeOfAKindMultipliers =
        {
            50, // Seven
            20, // Bar
            10, // Bell
            5   // Cherry
        };

        /// <summary>Bonus multiplier awarded for exactly two cherries.</summary>
        public const int TwoCherriesMultiplier = 2;

        /// <summary>
        /// Evaluates the stopped reels and returns the win for this spin.
        /// </summary>
        /// <param name="results">One symbol per reel (left to right).</param>
        public static WinResult Evaluate(SymbolType[] results)
        {
            if (results == null || results.Length == 0)
                return WinResult.NoWin;

            // Core win condition: every reel shows the same symbol.
            bool allEqual = true;
            for (int i = 1; i < results.Length; i++)
            {
                if (results[i] != results[0])
                {
                    allEqual = false;
                    break;
                }
            }

            if (allEqual)
            {
                SymbolType s = results[0];
                int multiplier = ThreeOfAKindMultipliers[(int)s];

                if (s == SymbolType.Seven)
                    return new WinResult(WinKind.Jackpot, s, multiplier, "JACKPOT! SEVENS x" + multiplier);

                return new WinResult(WinKind.ThreeOfAKind, s, multiplier,
                    "THREE " + s.ToString().ToUpperInvariant() + "S x" + multiplier);
            }

            // Bonus rule: exactly two cherries pay a small consolation prize.
            int cherryCount = 0;
            foreach (SymbolType s in results)
            {
                if (s == SymbolType.Cherry)
                    cherryCount++;
            }
            if (cherryCount == 2)
            {
                return new WinResult(WinKind.Pair, SymbolType.Cherry,
                    TwoCherriesMultiplier, "TWO CHERRIES x" + TwoCherriesMultiplier);
            }

            return WinResult.NoWin;
        }

        /// <summary>Convenience helper: payout in credits for a given bet.</summary>
        public static int PayoutFor(WinResult result, int bet)
        {
            return result.Multiplier * bet;
        }
    }
}
