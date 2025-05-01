using UnityEngine;
using Dokkaebi.Utilities;

namespace Dokkaebi.Camera
{
    /// <summary>
    /// Controls the camera movement and zoom for the Dokkaebi game
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 20f;
        [SerializeField] private float edgeScrollMargin = 20f;
        [SerializeField] private bool useEdgeScrolling = true;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 10f;
        [SerializeField] private float minZoom = 5f;
        [SerializeField] private float maxZoom = 20f;

        [Header("Rotation Settings")]
        [SerializeField] private float rotationSpeed = 100f;
        [SerializeField] private bool allowRotation = true;
        [SerializeField] private float defaultOrbitDistance = 10f;

        [Header("Follow Settings")]
        [SerializeField] private bool followSelectedUnit = true;
        [SerializeField] private float followSpeed = 5f;
        [SerializeField] private Vector3 followOffset = new Vector3(0, 10, -10);

        [Header("Orbit Settings")]
        [SerializeField] private Vector3 defaultOrbitPivot = Vector3.zero;

        private Transform cameraTransform;
        private Vector3 targetPosition;
        private float targetZoom;
        private bool isFollowingUnit = false;
        private Transform unitToFollow;

        // Store initial position and rotation
        private Vector3 initialPosition;
        private Quaternion initialRotation;

        // Orbit state
        private float currentOrbitAngleY;
        private float initialHorizontalDistance;
        private float initialHeightDifference;
        private bool isOrbitRotationActive = false;
        private float currentInputRotationAmount = 0f;
        private Transform orbitPivotUnitTransform; // The actual transform being orbited around
        private bool isPerformingOrbit = false; // Flag for active orbit control

        // --- Smooth Orbit Stop State (Removed Entirely) ---
        // private bool isStoppingOrbit = false; // Flag to indicate smooth stop is active
        // private Vector3 orbitRestPosition; // The final target position when stopped
        // private Quaternion orbitRestRotation; // The final target rotation when stopped
        // private Vector3 velocity = Vector3.zero; // For SmoothDamp position smoothing
        // [SerializeField] private float smoothTime = 0.1f; // Approx time to reach target position

        public float RotationSpeed => rotationSpeed;
        public bool AllowRotation => allowRotation;
        public Vector3 DefaultOrbitPivot => defaultOrbitPivot; // Public getter for InputManager

        /// <summary>
        /// Sets the continuous rotation input amount received from InputManager.
        /// </summary>
        public void SetOrbitRotationInputAmount(float amount)
        {
            currentInputRotationAmount = amount;
        }

        private void Awake()
        {
            cameraTransform = transform;
            SmartLogger.Log($"[CameraController] Awake - Initial Position: {cameraTransform.position}", LogCategory.General, this);
            targetPosition = cameraTransform.position;
            targetZoom = cameraTransform.position.y;
            // Store initial position and rotation
            initialPosition = cameraTransform.position;
            initialRotation = cameraTransform.rotation;
        }

        private void Start()
        {
            // Log the stored initial position
            SmartLogger.Log($"[CameraController] Start - Stored Initial Position: {initialPosition}", LogCategory.General, this);
        }

        private void Update()
        {
            // Only perform movement, zoom, or follow if NOT performing orbit
            if (isPerformingOrbit) return;

            HandleMovementInput();
            HandleZoomInput();
            // HandleRotationInput(); // Commented out, replaced by orbit logic

            // Only follow if isFollowingUnit is true AND we are not performing orbit
            if (isFollowingUnit && unitToFollow != null)
            {
                FollowUnit();
            }
            else
            {
                 // Apply movement and zoom when not following or orbiting
                 Vector3 currentPos = cameraTransform.position;
                 currentPos = Vector3.Lerp(currentPos, new Vector3(targetPosition.x, currentPos.y, targetPosition.z), Time.deltaTime * moveSpeed);
                 currentPos.y = Mathf.Lerp(currentPos.y, targetZoom, Time.deltaTime * zoomSpeed);
                 cameraTransform.position = currentPos;
            }
        }

        private void LateUpdate()
        {
             // Log state at the beginning of LateUpdate
            SmartLogger.Log($"[CameraController.LateUpdate] Start Frame. isPerformingOrbit: {isPerformingOrbit}, isOrbitRotationActive: {isOrbitRotationActive}", LogCategory.General, this);

             // Determine the pivot point for ORBITING
            Vector3 pivotPoint = (orbitPivotUnitTransform != null) ? orbitPivotUnitTransform.position : defaultOrbitPivot;

            if (isOrbitRotationActive) // Continue orbiting while rotation is active (started by StartOrbitRotation)
            {
                // Continuous orbit calculation using the amount set externally by SetOrbitRotationInputAmount
                currentOrbitAngleY += currentInputRotationAmount;

                // Optional: Keep angle within 0-360 for cleaner values, though not strictly necessary for calculation
                // currentOrbitAngleY = Mathf.Repeat(currentOrbitAngleY, 360f);

                float radians = currentOrbitAngleY * Mathf.Deg2Rad;
                float x = pivotPoint.x + initialHorizontalDistance * Mathf.Sin(radians);
                float z = pivotPoint.z + initialHorizontalDistance * Mathf.Cos(radians);
                float y = pivotPoint.y + initialHeightDifference; // Use the initial height difference

                Vector3 newCameraPosition = new Vector3(x, y, z);
                cameraTransform.position = newCameraPosition;

                // Ensure the camera always looks at the pivot point
                cameraTransform.LookAt(pivotPoint);

                // isPerformingOrbit is already true if isOrbitRotationActive is true
                // isStoppingOrbit and velocity related to smooth stop are removed.

                // Log orbiting state after applying position and rotation
                 SmartLogger.Log($"[CameraController.LateUpdate] Orbiting. AngleY: {currentOrbitAngleY:F2}, Pos: {cameraTransform.position}, Rot: {cameraTransform.rotation.eulerAngles}", LogCategory.General, this);
            }
             // No else if for isStoppingOrbit needed for immediate stop
             else
             {
                 // If not orbiting, ensure isPerformingOrbit is false.
                 // This handles the state after StopOrbitRotation is called.
                 isPerformingOrbit = false;
                 // No need for a separate stopping state cleanup here.
                 // The camera simply stays at its last position and rotation.
             }

            // IMPORTANT: Ensure other LateUpdate logic (if any) doesn't run if orbiting
            // if (!isOrbitRotationActive)
            // {
                 // Add any other non-orbit LateUpdate logic here if needed
            // }
        }


        /// <summary>
        /// Handle keyboard and mouse edge scrolling for camera movement
        /// </summary>
        private void HandleMovementInput()
        {
            // Return early if following a unit (handled in Update's main if/else)
            // if (isFollowingUnit && unitToFollow != null)
            //     return;

            Vector3 moveDir = Vector3.zero;

            // Keyboard movement
            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
                moveDir += transform.forward;

            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
                moveDir -= transform.forward;

            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
                moveDir -= transform.right;

            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
                moveDir += transform.right;

            // Edge scrolling
            if (useEdgeScrolling)
            {
                Vector3 mousePos = UnityEngine.Input.mousePosition;

                if (mousePos.x < edgeScrollMargin)
                    moveDir -= transform.right;

                if (mousePos.x > Screen.width - edgeScrollMargin)
                    moveDir += transform.right;

                if (mousePos.y < edgeScrollMargin)
                    moveDir -= transform.forward;

                if (mousePos.y > Screen.height - edgeScrollMargin)
                    moveDir += transform.forward;
            }

            // Normalize and apply movement
            if (moveDir.magnitude > 0)
            {
                moveDir.Normalize();
                targetPosition += moveDir * moveSpeed * Time.deltaTime;
            }
        }

        /// <summary>
        /// Handle mouse wheel for camera zoom
        /// </summary>
        private void HandleZoomInput()
        {
            float scrollWheel = UnityEngine.Input.GetAxis("Mouse ScrollWheel");

            if (scrollWheel != 0)
            {
                targetZoom -= scrollWheel * zoomSpeed;
                targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
            }
        }

        /// <summary>
        /// Follow a specific unit
        /// </summary>
        private void FollowUnit()
        {
            // Already checked for isPerformingOrbit in Update before calling this
            if (unitToFollow == null)
            {
                isFollowingUnit = false;
                return;
            }
            Vector3 targetPos = unitToFollow.position + followOffset;
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);
            // transform.LookAt(unitToFollow.position); // Optional: make camera look at the unit's base while following
            // SmartLogger.Log($"[CameraController] FollowUnit - Position: {transform.position}", LogCategory.General, this);
        }

        /// <summary>
        /// Set the unit to follow
        /// </summary>
        public void SetFollowTarget(Transform unit)
        {
             SmartLogger.Log($"[CameraController] SetFollowTarget called. Unit: {(unit != null ? unit.name : "None")}", LogCategory.General, this);

            // When setting a new follow target, STOP orbiting
            StopOrbitRotation(); // Ensure orbit stops when starting follow

            if (unit != null && followSelectedUnit)
            {
                unitToFollow = unit;
                isFollowingUnit = true;
                // DO NOT calculate orbit parameters here.
                // They will be calculated in StartOrbitRotation when orbiting explicitly begins.
                SmartLogger.Log($"[CameraController] SetFollowTarget - Following {unit.name} - Target Position (Follow Offset): {unit.position + followOffset}", LogCategory.General, this);

                 // Optionally, set initial target position for smooth transition to follow
                 // targetPosition = transform.position; // Or unit.position + followOffset;
            }
            else
            {
                unitToFollow = null;
                isFollowingUnit = false;
                // If we stop following, perhaps reset targetPosition to current camera position
                targetPosition = transform.position;
            }
        }

        /// <summary>
        /// Focus camera on a specific world position
        /// </summary>
        public void FocusOnWorldPosition(Vector3 worldPosition)
        {
             SmartLogger.Log($"[CameraController] FocusOnWorldPosition called. Position: {worldPosition}", LogCategory.General, this);
            // Stop following any unit
            StopFollowing();
            // Stop orbiting
            StopOrbitRotation();

            // Set target position
            targetPosition = worldPosition;
             // Keep current zoom level
             targetZoom = cameraTransform.position.y; // Or a desired default zoom level
        }

        /// <summary>
        /// Stop following the current unit
        /// </summary>
        public void StopFollowing()
        {
             SmartLogger.Log($"[CameraController] StopFollowing called.", LogCategory.General, this);
            isFollowingUnit = false;
            unitToFollow = null;
            // When stopping following, the camera should likely stay where it is
            // and allow free movement/orbit from that point.
            targetPosition = cameraTransform.position; // Set target position to current pos
        }

        /// <summary>
        /// Starts the orbital rotation around the pivot.
        /// This method is called once when orbit input begins.
        /// </summary>
        /// <param name="pivotUnitTransform">The transform of the unit to orbit around, or null for default pivot.</param>
        public void StartOrbitRotation(Transform pivotUnitTransform) // Removed rotationAmount parameter, input is continuous
        {
             SmartLogger.Log($"[CameraController] StartOrbitRotation called. Pivot Unit: {(pivotUnitTransform != null ? pivotUnitTransform.name : "None")}", LogCategory.General, this);

            // Stop following when starting orbit
            StopFollowing(); // Ensure follow is off

            // isStoppingOrbit and velocity related to smooth stop are removed.

            isPerformingOrbit = true; // Indicate that we are actively controlling orbit
            orbitPivotUnitTransform = pivotUnitTransform; // Set the pivot transform
            isOrbitRotationActive = true; // Indicate that orbit rotation calculation should run

             // *** Recalculate initial state relative to the chosen pivot at the moment orbit starts ***
             Vector3 currentCameraPosition = cameraTransform.position;
             Vector3 pivotPosition = (orbitPivotUnitTransform != null) ? orbitPivotUnitTransform.position : defaultOrbitPivot;

             Vector3 pivotToCamera = currentCameraPosition - pivotPosition;

             // Calculate initial height difference
             initialHeightDifference = pivotToCamera.y;

             // Calculate initial horizontal distance
             Vector3 horizontalOffset = new Vector3(pivotToCamera.x, 0, pivotToCamera.z);
             initialHorizontalDistance = horizontalOffset.magnitude;

             // Calculate the initial angle relative to the pivot on the horizontal plane
             // Atan2(y, x) gives the angle in radians between the x-axis and a point (x, y)
             // We use Z for the 'forward' direction and X for the 'right' direction in horizontal plane
             currentOrbitAngleY = Mathf.Atan2(pivotToCamera.x, pivotToCamera.z) * Mathf.Rad2Deg;


             SmartLogger.Log($"[CameraController] StartOrbitRotation - Calculated initial state relative to pivot {pivotPosition}: Angle={currentOrbitAngleY:F2}, H_Dist={initialHorizontalDistance:F2}, H_Diff={initialHeightDifference:F2}", LogCategory.General, this);


            // The continuous rotation amount is set by InputManager via SetOrbitRotationInputAmount
            // currentInputRotationAmount is already being updated by InputManager
            // No need to set initial currentInputRotationAmount here based on key press direction,
            // as InputManager will immediately start sending the continuous amount.


            SmartLogger.Log($"[CameraController] StartOrbitRotation END. State: isPerformingOrbit={isPerformingOrbit}, isOrbitRotationActive={isOrbitRotationActive}", LogCategory.General, this);
        }

        /// <summary>
        /// Stops the orbital rotation immediately.
        /// This method is called once when orbit input ends.
        /// </summary>
        public void StopOrbitRotation()
        {
             SmartLogger.Log($"[CameraController] StopOrbitRotation called.", LogCategory.General, this);

            // Set flags to immediately stop orbit calculation in LateUpdate
            isPerformingOrbit = false;
            isOrbitRotationActive = false;
            // isStoppingOrbit and velocity related to smooth stop are removed.

            currentInputRotationAmount = 0f; // Zero out any pending rotation input

            // If you want the camera to stay exactly where it stopped,
            // you don't need to set a target position here unless transitioning
            // to a different state like free movement.
            // targetPosition = cameraTransform.position; // Optional: Set target for free movement from current stop pos
            // targetZoom = cameraTransform.position.y; // Optional: Set target zoom

            SmartLogger.Log($"[CameraController] StopOrbitRotation END. State: isPerformingOrbit={isPerformingOrbit}, isOrbitRotationActive={isOrbitRotationActive}", LogCategory.General, this);
        }

        /// <summary>
        /// Reset camera to its initial position and rotation, and stop following/orbiting
        /// </summary>
        public void ResetToInitialPosition()
        {
             SmartLogger.Log($"[CameraController] ResetToInitialPosition called.", LogCategory.General, this);
            // Stop following and orbiting
            StopFollowing();
            StopOrbitRotation(); // Ensure orbit flags are reset

            // Immediately set position and rotation
            cameraTransform.position = initialPosition;
            cameraTransform.rotation = initialRotation;

            // Also set target position and zoom for smooth transition if free movement starts next
            targetPosition = initialPosition;
            targetZoom = initialPosition.y;

            // Recalculate initial orbit parameters relative to default pivot if needed for future orbits
            // (This part can stay, but ensure it doesn't interfere with the current state)
            Vector3 pivotToCameraInitial = initialPosition - defaultOrbitPivot;
            initialHeightDifference = pivotToCameraInitial.y;
            Vector3 horizontalOffsetInitial = new Vector3(pivotToCameraInitial.x, 0, pivotToCameraInitial.z);
            initialHorizontalDistance = horizontalOffsetInitial.magnitude;
            currentOrbitAngleY = Mathf.Atan2(pivotToCameraInitial.x, pivotToCameraInitial.z) * Mathf.Rad2Deg;


            SmartLogger.Log($"[CameraController] ResetToInitialPosition - Camera position: {cameraTransform.position}, Rotation: {cameraTransform.rotation}", LogCategory.General, this);
        }


        // Removed unused HandleRotationInput method

        // Removed smooth stop related variables and logic

    }
}