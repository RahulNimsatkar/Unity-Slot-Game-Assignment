using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SlotGame
{
    /// <summary>
    /// The mechanical lever on the right side of the machine.
    ///
    /// The lever art ships as two full-machine overlays (ball up / ball down)
    /// that align perfectly when stacked, so pulling the lever is simply a
    /// timed swap between the two overlays plus a small squash for weight.
    /// Clicking the lever starts a spin, just like a real one-armed bandit.
    /// </summary>
    public class LeverController : MonoBehaviour, IPointerClickHandler
    {
        [Header("Wiring (assigned by the scene builder)")]
        [Tooltip("Overlay object showing the lever at rest (ball up).")]
        [SerializeField] private GameObject _leverUp;

        [Tooltip("Overlay object showing the lever pulled (ball down).")]
        [SerializeField] private GameObject _leverDown;

        [Tooltip("The machine to start when the lever is pulled.")]
        [SerializeField] private SlotMachineController _machine;

        [Tooltip("Optional audio feedback.")]
        [SerializeField] private AudioManager _audio;

        [Tooltip("Art object squashed during the pull. Defaults to this object when empty.")]
        [SerializeField] private RectTransform _squashTarget;

        [Header("Tuning")]
        [Tooltip("Seconds the lever stays down before springing back.")]
        [SerializeField] private float _pullDuration = 0.35f;

        private bool _isPulling;

        /// <summary>Pointer handler: pull the lever to spin.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isPulling || _machine == null || _machine.IsSpinning)
                return;

            StartCoroutine(PullRoutine());
        }

        private IEnumerator PullRoutine()
        {
            _isPulling = true;

            if (_audio != null)
                _audio.PlayLever();

            RectTransform squash = _squashTarget != null
                ? _squashTarget
                : transform as RectTransform;

            // Ball down + slight squash gives the pull some weight.
            SetLeverState(pulled: true);
            if (squash != null)
                squash.localScale = new Vector3(1f, 0.97f, 1f);

            yield return new WaitForSeconds(_pullDuration);

            SetLeverState(pulled: false);
            if (squash != null)
                squash.localScale = Vector3.one;

            _machine.TrySpin();
            _isPulling = false;
        }

        private void SetLeverState(bool pulled)
        {
            if (_leverUp != null) _leverUp.SetActive(!pulled);
            if (_leverDown != null) _leverDown.SetActive(pulled);
        }
    }
}
