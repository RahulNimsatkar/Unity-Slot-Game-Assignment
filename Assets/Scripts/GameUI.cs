using SlotGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame
{
    /// <summary>
    /// Binds the HUD (credits / bet / win readouts, message line, spin and bet
    /// buttons) to the game logic. Pure view layer: it only *observes* the
    /// <see cref="SlotMachineController"/> and <see cref="Wallet"/> through
    /// their events and never mutates game state directly.
    ///
    /// All button listeners are wired at runtime in <see cref="Start"/> so the
    /// scene file stays free of fragile persistent bindings.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("Game references (assigned by the scene builder)")]
        [SerializeField] private SlotMachineController _machine;
        [SerializeField] private AudioManager _audio;

        [Header("HUD texts")]
        [SerializeField] private Text _creditsValue;
        [SerializeField] private Text _betValue;
        [SerializeField] private Text _winValue;
        [SerializeField] private Text _messageText;
        [SerializeField] private Text _winText;

        [Header("Buttons")]
        [SerializeField] private Button _spinButton;
        [SerializeField] private Button _betUpButton;
        [SerializeField] private Button _betDownButton;

        [Header("Extras")]
        [Tooltip("Out-of-credits popup.")]
        [SerializeField] private PopupController _popup;

        [Tooltip("Pulse animation played on the win text for every win.")]
        [SerializeField] private Animation _winPulse;

        private const string DefaultMessage = "PLACE YOUR BET AND PULL THE LEVER";

        private void Start()
        {
            if (_machine == null)
            {
                Debug.LogError(name + ": SlotMachineController reference missing.", this);
                enabled = false;
                return;
            }

            // Wallet events -> readouts.
            _machine.Wallet.CreditsChanged += OnCreditsChanged;
            _machine.Wallet.BetChanged += OnBetChanged;

            // Controller events -> messages and popups.
            _machine.SpinStarted += OnSpinStarted;
            _machine.SpinEvaluated += OnSpinEvaluated;
            _machine.FreeSpinAwarded += OnFreeSpinAwarded;
            _machine.OutOfCredits += OnOutOfCredits;

            // Button wiring (runtime listeners, see class summary).
            if (_spinButton != null)
                _spinButton.onClick.AddListener(() => { PlayClick(); _machine.TrySpin(); });
            if (_betUpButton != null)
                _betUpButton.onClick.AddListener(() => { PlayClick(); _machine.Wallet.IncreaseBet(); });
            if (_betDownButton != null)
                _betDownButton.onClick.AddListener(() => { PlayClick(); _machine.Wallet.DecreaseBet(); });

            // Initial readouts.
            OnCreditsChanged(_machine.Wallet.Credits);
            OnBetChanged(_machine.Wallet.CurrentBet);
            SetWinText(0);
            ShowMessage(DefaultMessage);
        }

        private void Update()
        {
            // Spin is only allowed while idle; bet changes are locked mid-spin
            // so the payout always matches the bet that started the spin.
            bool idle = !_machine.IsSpinning;
            if (_spinButton != null)
                _spinButton.interactable = idle && _machine.Wallet.CanAffordSpin;
            if (_betUpButton != null)
                _betUpButton.interactable = idle;
            if (_betDownButton != null)
                _betDownButton.interactable = idle;
        }

        // ------------------------------------------------------------------ events

        private void OnCreditsChanged(int credits) => SetText(_creditsValue, credits.ToString());

        private void OnBetChanged(int bet) => SetText(_betValue, bet.ToString());

        private void OnSpinStarted(bool freeSpin)
        {
            SetWinText(0);
            ShowMessage(freeSpin ? "BONUS FREE SPIN!" : "SPINNING...");
        }

        private void OnSpinEvaluated(WinResult result, int payout, bool freeSpin)
        {
            if (result.Kind == WinKind.None)
            {
                ShowMessage("NO WIN - TRY AGAIN");
                SetWinText(0);
                return;
            }

            ShowMessage(result.Label + (freeSpin ? " (FREE SPIN)" : string.Empty));
            SetWinText(payout);

            // Celebrate: pulse the win amount.
            if (_winPulse != null)
            {
                _winPulse.Stop();
                _winPulse.Play();
            }
        }

        private void OnFreeSpinAwarded()
        {
            ShowMessage("LUCKY! BONUS FREE SPIN AWARDED");
        }

        private void OnOutOfCredits()
        {
            if (_popup == null)
                return;

            _popup.Show(
                "OUT OF CREDITS",
                "Refill your balance to " + Wallet.StartingCredits + " credits?",
                onYes: () => { _machine.Wallet.Refill(); ShowMessage(DefaultMessage); },
                onNo: () => ShowMessage("NO CREDITS LEFT - PRESS YES NEXT TIME TO REFILL"));
        }

        // ----------------------------------------------------------------- helpers

        private void SetWinText(int amount)
        {
            SetText(_winValue, amount.ToString());
            if (_winText != null)
                _winText.text = amount > 0 ? "WIN " + amount : string.Empty;
        }

        private void ShowMessage(string message) => SetText(_messageText, message);

        private void PlayClick()
        {
            if (_audio != null)
                _audio.PlayClick();
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
                target.text = value;
        }
    }
}
