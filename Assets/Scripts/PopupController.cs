using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame
{
    /// <summary>
    /// Generic Yes/No popup panel (uses the popup art and the Yes_No button sheet).
    ///
    /// Shown when the player runs out of credits: "Yes" refills the wallet,
    /// "No" (or the X close button) dismisses the popup without refilling -
    /// in that case the popup simply reappears on the next spin attempt.
    /// The panel pops in with a small elastic scale tween for game feel.
    /// </summary>
    public class PopupController : MonoBehaviour
    {
        [Header("Wiring (assigned by the scene builder)")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _messageText;
        [SerializeField] private Button _yesButton;
        [SerializeField] private Button _noButton;
        [SerializeField] private Button _closeButton;

        [Tooltip("Target of the pop-in animation. Defaults to this object when empty.")]
        [SerializeField] private RectTransform _animatedTarget;

        [Header("Tuning")]
        [Tooltip("Duration of the scale-in animation in seconds.")]
        [SerializeField] private float _showDuration = 0.25f;

        private Action _onYes;
        private Action _onNo;
        private RectTransform _rect;
        private Coroutine _tween;

        private void Awake()
        {
            _rect = _animatedTarget != null ? _animatedTarget : transform as RectTransform;

            // Runtime listener wiring keeps the scene file free of fragile bindings.
            if (_yesButton != null)
                _yesButton.onClick.AddListener(HandleYes);
            if (_noButton != null)
                _noButton.onClick.AddListener(HandleNo);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(HandleNo);
        }

        /// <summary>Shows the popup with the given content and callbacks.</summary>
        public void Show(string title, string message, Action onYes, Action onNo)
        {
            _onYes = onYes;
            _onNo = onNo;

            if (_titleText != null) _titleText.text = title;
            if (_messageText != null) _messageText.text = message;

            gameObject.SetActive(true);

            if (_tween != null) StopCoroutine(_tween);
            _tween = StartCoroutine(ScaleRoutine(Vector3.zero, Vector3.one));
        }

        /// <summary>Hides the popup immediately.</summary>
        public void Hide()
        {
            if (_tween != null) StopCoroutine(_tween);
            _tween = null;
            gameObject.SetActive(false);
        }

        private void HandleYes()
        {
            Hide();
            _onYes?.Invoke();
        }

        private void HandleNo()
        {
            Hide();
            _onNo?.Invoke();
        }

        // Simple overshoot ease: scale 0 -> 1.08 -> 1 for a snappy "pop".
        private IEnumerator ScaleRoutine(Vector3 from, Vector3 to)
        {
            float elapsed = 0f;
            while (elapsed < _showDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(elapsed / _showDuration);
                // Back-out easing (overshoot then settle).
                float s = 1f + 2.7f * Mathf.Pow(u - 1f, 3f) + 1.7f * Mathf.Pow(u - 1f, 2f);
                if (_rect != null)
                    _rect.localScale = Vector3.LerpUnclamped(from, to, Mathf.Max(0f, s));
                yield return null;
            }
            if (_rect != null)
                _rect.localScale = to;
            _tween = null;
        }
    }
}
