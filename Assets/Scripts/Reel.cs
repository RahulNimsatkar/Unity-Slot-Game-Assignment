using System;
using System.Collections;
using System.Collections.Generic;
using SlotGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SlotGame
{
    /// <summary>
    /// A single slot reel: a masked window through which a vertical strip of
    /// symbol images scrolls downward, casino-style.
    ///
    /// Spin model (phases driven by one coroutine):
    ///   1. Accelerate up to max speed.
    ///   2. Cruise at max speed for the requested duration.
    ///   3. Land: the next symbol that wraps to the top of the strip becomes the
    ///      "landing symbol" and receives the target sprite chosen by the RNG.
    ///      The remaining travel distance to center it is known exactly, so the
    ///      reel eases out (cubic) onto the target and finishes with a small
    ///      rubber-band bounce. Deterministic landing = no visible snapping.
    ///
    /// Symbols leaving the bottom of the window recycle to the top with a fresh
    /// random sprite, giving the illusion of an endless reel strip.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class Reel : MonoBehaviour
    {
        [Header("Wiring (assigned by the scene builder)")]
        [Tooltip("RectTransform holding every symbol image, moved vertically while spinning.")]
        [SerializeField] private RectTransform _strip;

        [Tooltip("The symbol images inside the strip.")]
        [SerializeField] private Image[] _symbolImages;

        [Header("Tuning")]
        [Tooltip("Vertical distance between two neighbour symbol centers, in local units.")]
        [SerializeField] private float _spacing = 100f;

        [Tooltip("Top scroll speed in local units per second.")]
        [SerializeField] private float _maxSpeed = 1600f;

        [Tooltip("Seconds spent ramping from 0 to max speed.")]
        [SerializeField] private float _accelerationTime = 0.3f;

        [Tooltip("Height of the landing bounce overshoot, in local units.")]
        [SerializeField] private float _bounceHeight = 12f;

        [Tooltip("Duration of the landing bounce in seconds.")]
        [SerializeField] private float _bounceDuration = 0.18f;

        [Tooltip("Half height of the visible window (used to find the recycle line).")]
        [SerializeField] private float _windowHalfHeight = 90f;

        /// <summary>Raised once when this reel has fully stopped (after the bounce).</summary>
        public event Action<Reel> Stopped;

        /// <summary>True while any spin phase is running.</summary>
        public bool IsSpinning { get; private set; }

        private Dictionary<SymbolType, Sprite> _spriteBySymbol;
        private SlotRNG _rng;

        // Symbols are recycled when their center passes this local Y line.
        private float RecycleY => -(_windowHalfHeight + _spacing * 0.5f);

        // Total height of the recycled strip.
        private float TotalHeight => _symbolImages.Length * _spacing;

        private void Awake()
        {
            // Defensive wiring so the component also works when dropped in by hand.
            if (_strip == null && transform.childCount > 0)
                _strip = transform.GetChild(0) as RectTransform;
        }

        /// <summary>
        /// Provides the reel with its sprite table and the shared RNG.
        /// Must be called before <see cref="SnapTo"/> or <see cref="Spin"/>.
        /// </summary>
        public void Configure(SymbolDefinition[] definitions, SlotRNG rng)
        {
            if (definitions == null || definitions.Length == 0)
            {
                Debug.LogError(name + ": Reel needs at least one symbol definition.", this);
                return;
            }

            _spriteBySymbol = new Dictionary<SymbolType, Sprite>(definitions.Length);
            foreach (SymbolDefinition def in definitions)
            {
                if (def == null || def.Sprite == null)
                {
                    Debug.LogError(name + ": Symbol definition or sprite missing.", this);
                    continue;
                }
                _spriteBySymbol[def.Type] = def.Sprite;
            }
            _rng = rng;
        }

        /// <summary>
        /// Instantly places the reel at rest with the given symbol centered.
        /// Used to dress the machine before the first spin.
        /// </summary>
        public void SnapTo(SymbolType symbol)
        {
            if (!IsReady()) return;

            SetStripY(0f);
            NormalizePositions();

            int middle = _symbolImages.Length / 2;
            for (int i = 0; i < _symbolImages.Length; i++)
                SetSprite(_symbolImages[i], i == middle ? symbol : _rng.NextSymbol());

            SetStripY(-_symbolImages[middle].rectTransform.anchoredPosition.y);
            ResetStretch();
        }

        /// <summary>
        /// Spins the reel and lands it on the requested symbol.
        /// </summary>
        /// <param name="target">Symbol that must be centered when the reel stops.</param>
        /// <param name="spinDuration">Seconds spent at full speed before landing begins.</param>
        /// <param name="stopDuration">Seconds spent easing onto the target.</param>
        public void Spin(SymbolType target, float spinDuration, float stopDuration)
        {
            if (!IsReady() || IsSpinning)
                return;

            StartCoroutine(SpinRoutine(target, spinDuration, stopDuration));
        }

        private bool IsReady()
        {
            if (_strip == null || _symbolImages == null || _symbolImages.Length == 0)
            {
                Debug.LogError(name + ": Reel is missing its strip or symbol images.", this);
                return false;
            }
            if (_spriteBySymbol == null || _rng == null)
            {
                Debug.LogError(name + ": Reel.Configure must be called first.", this);
                return false;
            }
            return true;
        }

        private IEnumerator SpinRoutine(SymbolType target, float spinDuration, float stopDuration)
        {
            IsSpinning = true;
            float dt;

            // --- Phase 1: accelerate ------------------------------------------------
            float elapsed = 0f;
            while (elapsed < _accelerationTime)
            {
                dt = Time.deltaTime;
                elapsed += dt;
                float speed = Mathf.SmoothStep(0f, _maxSpeed,
                    Mathf.Clamp01(elapsed / _accelerationTime));
                MoveStrip(speed * dt, null);
                ApplyStretch(speed);
                yield return null;
            }

            // --- Phase 2: cruise at max speed ---------------------------------------
            elapsed = 0f;
            while (elapsed < spinDuration)
            {
                dt = Time.deltaTime;
                elapsed += dt;
                MoveStrip(_maxSpeed * dt, null);
                ApplyStretch(_maxSpeed);
                yield return null;
            }

            // --- Phase 3a: arm the landing symbol -----------------------------------
            // Keep cruising until some symbol wraps to the top; that symbol receives
            // the target sprite and becomes the one we land on.
            Image landing = null;
            while (landing == null)
            {
                dt = Time.deltaTime;
                landing = MoveStrip(_maxSpeed * dt, target);
                ApplyStretch(_maxSpeed);
                yield return null;
            }

            // --- Phase 3b: ease-out over the exact remaining distance ---------------
            // The landing symbol travels less than one full strip height before it is
            // centered, so it can never be recycled again before landing.
            float startStripY = _strip.anchoredPosition.y;
            float finalStripY = -landing.rectTransform.anchoredPosition.y;
            float distance = startStripY - finalStripY;

            elapsed = 0f;
            while (elapsed < stopDuration)
            {
                dt = Time.deltaTime;
                elapsed += dt;
                float u = Mathf.Clamp01(elapsed / stopDuration);
                float eased = 1f - Mathf.Pow(1f - u, 3f); // cubic ease-out
                float targetY = startStripY - distance * eased;
                float delta = _strip.anchoredPosition.y - targetY;
                if (delta > 0f)
                    MoveStrip(delta, null);

                // Approximate current speed, only used for the stretch effect.
                ApplyStretch(distance * 3f * (1f - u) * (1f - u) / stopDuration);
                yield return null;
            }

            // Snap to the mathematically exact final position.
            float remaining = _strip.anchoredPosition.y - finalStripY;
            if (remaining > 0f)
                MoveStrip(remaining, null);

            // --- Phase 4: rubber-band bounce ----------------------------------------
            elapsed = 0f;
            while (elapsed < _bounceDuration)
            {
                dt = Time.deltaTime;
                elapsed += dt;
                float u = Mathf.Clamp01(elapsed / _bounceDuration);
                SetStripY(finalStripY - _bounceHeight * Mathf.Sin(Mathf.PI * u));
                ApplyStretch(0f);
                yield return null;
            }

            SetStripY(finalStripY);
            ResetStretch();

            IsSpinning = false;
            Stopped?.Invoke(this);
        }

        /// <summary>
        /// Moves the strip down by <paramref name="delta"/> and recycles any symbol
        /// that left the window back to the top.
        /// </summary>
        /// <param name="delta">Positive distance to travel downward.</param>
        /// <param name="armTarget">
        /// When set, the first recycled symbol receives this sprite and is returned
        /// (it becomes the landing symbol). When null, recycled symbols get random sprites.
        /// </param>
        /// <returns>The image that was armed as landing symbol, or null.</returns>
        private Image MoveStrip(float delta, SymbolType? armTarget)
        {
            Vector2 stripPos = _strip.anchoredPosition;
            stripPos.y -= delta;
            _strip.anchoredPosition = stripPos;

            Image newlyArmed = null;
            for (int i = 0; i < _symbolImages.Length; i++)
            {
                RectTransform symbolRect = _symbolImages[i].rectTransform;
                float centerY = symbolRect.anchoredPosition.y + stripPos.y;
                if (centerY < RecycleY)
                {
                    Vector2 p = symbolRect.anchoredPosition;
                    p.y += TotalHeight;
                    symbolRect.anchoredPosition = p;

                    if (armTarget.HasValue && newlyArmed == null)
                    {
                        SetSprite(_symbolImages[i], armTarget.Value);
                        newlyArmed = _symbolImages[i];
                    }
                    else
                    {
                        SetSprite(_symbolImages[i], _rng.NextSymbol());
                    }
                }
            }
            return newlyArmed;
        }

        /// <summary>
        /// Brings every symbol into the canonical range [RecycleY, RecycleY + TotalHeight)
        /// so the strip is in a consistent state before spinning.
        /// </summary>
        private void NormalizePositions()
        {
            float stripY = _strip.anchoredPosition.y;
            for (int i = 0; i < _symbolImages.Length; i++)
            {
                RectTransform symbolRect = _symbolImages[i].rectTransform;
                Vector2 p = symbolRect.anchoredPosition;
                while (p.y + stripY < RecycleY)
                    p.y += TotalHeight;
                while (p.y + stripY >= RecycleY + TotalHeight)
                    p.y -= TotalHeight;
                symbolRect.anchoredPosition = p;
            }
        }

        private void SetSprite(Image image, SymbolType symbol)
        {
            if (_spriteBySymbol.TryGetValue(symbol, out Sprite sprite))
                image.sprite = sprite;
        }

        private void SetStripY(float y)
        {
            Vector2 pos = _strip.anchoredPosition;
            pos.y = y;
            _strip.anchoredPosition = pos;
        }

        // Subtle motion blur: symbols stretch vertically with speed.
        private void ApplyStretch(float speed)
        {
            float f = Mathf.Clamp01(speed / _maxSpeed);
            Vector3 scale = new Vector3(1f - 0.07f * f, 1f + 0.22f * f, 1f);
            for (int i = 0; i < _symbolImages.Length; i++)
                _symbolImages[i].rectTransform.localScale = scale;
        }

        private void ResetStretch()
        {
            for (int i = 0; i < _symbolImages.Length; i++)
                _symbolImages[i].rectTransform.localScale = Vector3.one;
        }


    }
}
