using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ChaseGame.Input
{
    /// <summary>
    /// UI touch/drag zone component that converts pointer drag gestures into camera
    /// yaw (left/right) and pitch (up/down) rotation deltas.
    /// Attach this component to a UI RectTransform (e.g. a transparent Panel/Image
    /// covering half of the screen).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    // IPointerExitHandler is deliberately NOT implemented: a look pad must keep tracking while the
    // finger leaves its rect mid-drag (Unity keeps sending OnDrag to the handler that took the
    // pointer down), and the handler that used to be here threw NotImplementedException on every
    // exit. OnPointerUp ends the drag; that is the only end this pad needs.
    public class CameraLookTouchPad : MonoBehaviour,
        IPointerDownHandler,
        IDragHandler,
        IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;
        private const float ReferenceScreenHeight = 1080f;

        [Header("Sensitivity")]
        [Tooltip("Degrees of camera rotation (X = Yaw left/right, Y = Pitch up/down) per pixel at 1080p.")]
        [SerializeField] private Vector2 sensitivity = new Vector2(0.22f, 0.16f);

        [Tooltip("Scale drag delta by Screen.height so rotation speed feels consistent across resolutions/DPIs.")]
        [SerializeField] private bool normalizeByScreenHeight = true;

        [Header("Axis Inversion")]
        [SerializeField] private bool invertX = false;
        [SerializeField] private bool invertY = false;

        private int activePointerId = NoPointer;
        private Vector2 accumulatedLookDelta;

        /// <summary>
        /// Most recently enabled touchpad instance, allowing SimpleFollowCamera to read
        /// input automatically without expensive scene searches or manual Inspector wiring.
        /// </summary>
        public static CameraLookTouchPad ActiveInstance { get; private set; }

        /// <summary>
        /// True while a finger/pointer is actively holding and dragging this UI zone.
        /// </summary>
        public bool IsDragging => activePointerId != NoPointer;

        /// <summary>
        /// Current unconsumed rotation delta (x = yaw degrees, y = pitch degrees).
        /// </summary>
        public Vector2 PendingLookDelta => accumulatedLookDelta;

        private void Awake()
        {
            EnsureRaycastTarget();
        }

        private void OnEnable()
        {
            ActiveInstance = this;
        }

        private void OnDisable()
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            ResetPointerState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetPointerState();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null)
            {
                BeginTouch(eventData.pointerId);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData != null)
            {
                float screenHeight = normalizeByScreenHeight ? Screen.height : ReferenceScreenHeight;
                DragTouch(eventData.pointerId, eventData.delta, screenHeight);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null)
            {
                EndTouch(eventData.pointerId);
            }
        }

        public void OnPointerCancel(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerId == activePointerId)
            {
                ResetPointerState();
            }
        }

        public void BeginTouch(int pointerId)
        {
            // Lock onto the first pointer that touches this zone so multi-touch
            // (e.g. second thumb on the movement joystick) never causes camera jumps.
            if (activePointerId == NoPointer)
            {
                activePointerId = pointerId;
            }
        }

        public void DragTouch(int pointerId, Vector2 rawPixelDelta, float screenHeight = ReferenceScreenHeight)
        {
            if (pointerId != activePointerId)
            {
                return;
            }

            accumulatedLookDelta += ComputeLookDelta(
                rawPixelDelta,
                sensitivity,
                invertX,
                invertY,
                screenHeight);
        }

        public void EndTouch(int pointerId)
        {
            if (pointerId == activePointerId)
            {
                activePointerId = NoPointer;
            }
        }

        /// <summary>
        /// Returns the accumulated (yaw, pitch) delta in degrees since the last call
        /// and resets the internal accumulator to zero. Called by the camera in LateUpdate.
        /// </summary>
        public Vector2 ConsumeLookDelta()
        {
            Vector2 delta = accumulatedLookDelta;
            accumulatedLookDelta = Vector2.zero;
            return delta;
        }

        /// <summary>
        /// Pure calculation converting a raw pointer delta (in pixels) into (yaw, pitch) degrees.
        /// Dragging right (+x) rotates yaw right (+yaw).
        /// Dragging up (+y) tilts camera up (-pitch in Unity Euler X, or +pitch elevation depending on convention;
        /// here +y drag produces negative pitch delta so dragging up looks up, or positive to orbit higher).
        /// </summary>
        public static Vector2 ComputeLookDelta(
            Vector2 rawPixelDelta,
            Vector2 sensitivity,
            bool invertX,
            bool invertY,
            float screenHeight = ReferenceScreenHeight)
        {
            float scale = screenHeight > 1f ? ReferenceScreenHeight / screenHeight : 1f;

            float xSign = invertX ? -1f : 1f;
            // By default, dragging finger UP (+y) tilts view UP (decreases overhead elevation angle),
            // and dragging finger DOWN (-y) tilts view DOWN (increases overhead elevation angle).
            float ySign = invertY ? 1f : -1f;

            return new Vector2(
                rawPixelDelta.x * sensitivity.x * scale * xSign,
                rawPixelDelta.y * sensitivity.y * scale * ySign);
        }

        public void ResetPointerState()
        {
            activePointerId = NoPointer;
            accumulatedLookDelta = Vector2.zero;
        }

        // Ensures the UI object has a Graphic with raycastTarget enabled so Unity's
        // EventSystem will deliver pointer events even if the user created an empty UI object.
        private void EnsureRaycastTarget()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var graphic = GetComponent<Graphic>();
            if (graphic == null)
            {
                var image = gameObject.AddComponent<Image>();
                image.color = Color.clear;
                image.raycastTarget = true;
            }
            else if (!graphic.raycastTarget)
            {
                graphic.raycastTarget = true;
            }
        }

    }
}
