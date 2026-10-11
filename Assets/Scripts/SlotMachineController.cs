using System;
using System.Collections;
using SlotGame.Core;
using UnityEngine;

namespace SlotGame
{
    /// <summary>
    /// Central orchestrator of the game. Owns the <see cref="Wallet"/> and the
    /// <see cref="SlotRNG"/>, commands the reels, evaluates the pay table and
    /// broadcasts the outcome through C# events so the UI/audio layers stay
    /// decoupled from the game logic.
    ///
    /// Spin flow:
    ///   TrySpin -> deduct bet -> roll all results up front (RNG) ->
    ///   reels spin with staggered durations -> when all stopped ->
    ///   PayTable.Evaluate -> payout -> optional bonus free spin.
    /// </summary>
    public class SlotMachineController : MonoBehaviour
    {
        [Header("Game data")]
        [Tooltip("Sprite mapping for every symbol, indexed logically by SymbolType.")]
        [SerializeField] private SymbolDefinition[] _symbols;

        [Tooltip("The reels of the machine, in left-to-right order.")]
        [SerializeField] private Reel[] _reels;

        [Header("Spin timing")]
        [Tooltip("Full-speed seconds for the FIRST reel; later reels add the step.")]
        [SerializeField] private float _spinDurationBase = 1.0f;

        [Tooltip("Extra full-speed seconds added per reel (staggered stops).")]
        [SerializeField] private float _spinDurationStep = 0.5f;

        [Tooltip("Seconds each reel spends easing onto its final symbol.")]
        [SerializeField] private float _stopDuration = 0.45f;

        [Header("Bonus")]
        [Tooltip("Chance [0..1] that any winning spin awards a bonus free spin.")]
        [SerializeField, Range(0f, 1f)] private float _freeSpinChance = 0.12f;

        [Tooltip("Delay before an awarded free spin starts automatically.")]
        [SerializeField] private float _freeSpinDelay = 1.4f;

        [Header("Wiring (assigned by the scene builder)")]
        [SerializeField] private AudioManager _audio;

        /// <summary>Player wallet; exposed read-only so the UI can subscribe to it.</summary>
        public Wallet Wallet { get; private set; }

        /// <summary>True while any spin sequence is running.</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>Raised when a spin starts. Parameter = is it a free spin.</summary>
        public event Action<bool> SpinStarted;

        /// <summary>Raised when all reels stopped and the result is known.</summary>
        public event Action<WinResult, int, bool> SpinEvaluated; // result, payout, freeSpin

        /// <summary>Raised when a bonus free spin is awarded.</summary>
        public event Action FreeSpinAwarded;

        /// <summary>Raised when the player cannot afford the current bet.</summary>
        public event Action OutOfCredits;

        private SlotRNG _rng;
        private int _stoppedReelCount;

        private void Awake()
        {
            Wallet = new Wallet();
            _rng = SlotRNG.CreateDefault();
        }

        private void Start()
        {
            // Configure reels and dress the machine with a random resting position.
            foreach (Reel reel in _reels)
            {
                reel.Configure(_symbols, _rng);
                reel.Stopped += HandleReelStopped;
                reel.SnapTo(_rng.NextSymbol());
            }
        }

        /// <summary>
        /// Entry point for the spin button and the lever.
        /// Silently ignored while spinning; fires <see cref="OutOfCredits"/> when broke.
        /// </summary>
        public void TrySpin()
        {
            if (IsSpinning)
                return;

            if (!Wallet.CanAffordSpin)
            {
                OutOfCredits?.Invoke();
                return;
            }

            StartCoroutine(SpinSequence(freeSpin: false));
        }

        private void HandleReelStopped(Reel reel)
        {
            _stoppedReelCount++;
            if (_audio != null)
                _audio.PlayReelStop();
        }

        private IEnumerator SpinSequence(bool freeSpin)
        {
            IsSpinning = true;

            if (!freeSpin)
                Wallet.TryPlaceBet();

            SpinStarted?.Invoke(freeSpin);

            // Fairness note: the whole outcome is decided HERE, before any
            // animation starts. The reels merely animate towards it.
            SymbolType[] targets = _rng.NextSymbols(_reels.Length);

            _stoppedReelCount = 0;
            if (_audio != null)
                _audio.StartSpinLoop();

            for (int i = 0; i < _reels.Length; i++)
            {
                float spinDuration = _spinDurationBase + _spinDurationStep * i;
                _reels[i].Spin(targets[i], spinDuration, _stopDuration);
            }

            // Wait until every reel reported back through the Stopped event.
            while (_stoppedReelCount < _reels.Length)
                yield return null;

            if (_audio != null)
                _audio.StopSpinLoop();

            // Evaluate the outcome against the pay table.
            WinResult result = PayTable.Evaluate(targets);
            int payout = PayTable.PayoutFor(result, Wallet.CurrentBet);

            if (payout > 0)
            {
                Wallet.AddWinnings(payout);
                if (_audio != null)
                {
                    if (result.Kind == WinKind.Jackpot) _audio.PlayJackpot();
                    else _audio.PlayWin();
                }
            }
            else if (_audio != null)
            {
                _audio.PlayLose();
            }

            SpinEvaluated?.Invoke(result, payout, freeSpin);
            IsSpinning = false;

            // Bonus feature: winning spins can award a free spin.
            if (!freeSpin && result.Kind != WinKind.None && _rng.NextFloat() < _freeSpinChance)
            {
                FreeSpinAwarded?.Invoke();
                yield return new WaitForSeconds(_freeSpinDelay);
                StartCoroutine(SpinSequence(freeSpin: true));
                yield break;
            }

            // No free spin and no money left -> give the UI a beat, then notify.
            if (!Wallet.CanAffordSpin)
            {
                yield return new WaitForSeconds(0.8f);
                OutOfCredits?.Invoke();
            }
        }

    }
}
