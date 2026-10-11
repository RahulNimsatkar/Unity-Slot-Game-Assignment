using System;

namespace SlotGame.Core
{
    /// <summary>
    /// Holds the player's credits and the currently selected bet.
    ///
    /// Pure C# with C# events so the UI layer can subscribe without this class
    /// knowing anything about Unity. All mutations go through methods that fire
    /// change notifications - the UI never sets values directly.
    /// </summary>
    public sealed class Wallet
    {
        /// <summary>Credits the player starts (and refills) with.</summary>
        public const int StartingCredits = 1000;

        /// <summary>Available bet steps, matching the design mockup (Bet 10G / 50G / 100G).</summary>
        private static readonly int[] BetSteps = { 10, 50, 100 };

        /// <summary>Raised whenever the credit balance changes. Parameter = new balance.</summary>
        public event Action<int> CreditsChanged;

        /// <summary>Raised whenever the selected bet changes. Parameter = new bet.</summary>
        public event Action<int> BetChanged;

        public int Credits { get; private set; }
        public int CurrentBet => BetSteps[_betIndex];

        private int _betIndex;

        public Wallet()
        {
            Credits = StartingCredits;
            _betIndex = 0; // Default bet: 10
        }

        /// <summary>Raises the bet to the next step (wraps around at the top).</summary>
        public void IncreaseBet()
        {
            _betIndex = (_betIndex + 1) % BetSteps.Length;
            BetChanged?.Invoke(CurrentBet);
        }

        /// <summary>Lowers the bet to the previous step (wraps around at the bottom).</summary>
        public void DecreaseBet()
        {
            _betIndex = (_betIndex - 1 + BetSteps.Length) % BetSteps.Length;
            BetChanged?.Invoke(CurrentBet);
        }

        /// <summary>Returns true when the player can afford one more spin.</summary>
        public bool CanAffordSpin => Credits >= CurrentBet;

        /// <summary>
        /// Attempts to deduct the current bet for a spin.
        /// </summary>
        /// <returns>True when the bet was placed; false when funds are insufficient.</returns>
        public bool TryPlaceBet()
        {
            if (!CanAffordSpin)
                return false;

            Credits -= CurrentBet;
            CreditsChanged?.Invoke(Credits);
            return true;
        }

        /// <summary>Refunds the current bet (used for free spins).</summary>
        public void RefundBet()
        {
            Credits += CurrentBet;
            CreditsChanged?.Invoke(Credits);
        }

        /// <summary>Adds winnings to the balance.</summary>
        public void AddWinnings(int amount)
        {
            if (amount <= 0)
                return;

            Credits += amount;
            CreditsChanged?.Invoke(Credits);
        }

        /// <summary>Refills the balance back to the starting amount (used by the popup).</summary>
        public void Refill()
        {
            Credits = StartingCredits;
            CreditsChanged?.Invoke(Credits);
        }
    }
}
