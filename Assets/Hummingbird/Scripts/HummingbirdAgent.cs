using System;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

/// <summary>
/// A Humming Bird Machine Learning Agent
/// </summary>
public class HummingbirdAgent : Agent
{
    [Tooltip("Force to apply when moving")]
    public float moveForce = 2f;

    [Tooltip("Speed to pitch up or down")]
    public float pitchSpeed = 100f;

    [Tooltip("Speed to yaw/rotate about the up axix")]
    public float yawSpeed = 100f;

    [Tooltip("Transform at the Tip of the Beak")]
    public Transform beakTip;

    [Tooltip("Agent's Camera")]
    public Camera agentCamera;

    [Tooltip("Whether this is Training mode or Game Play mode")]
    public bool trainingMode;


    // Rigid Body of the Agent
    new private Rigidbody rigidbody;

    // Flower area that the agent is in
    private FlowerArea flowerArea;

    // Nearest flower to the agent
    private Flower nearestFlower;

    // Allows for smoother pitch changes
    private float smoothPitchChange = 0f;

    // Allows for smoother yaw changes
    private float smoothYawChange = 0f;

    // Maximum angle that the bird can pitch up or down
    private const float maxPitchAngle = 80f;

    // Maximum distance from the beak tip to accent nectar collision
    private const float beakTipRadius = 0.008f;

    // Whether the agent is frozen (Intentionally not flying)
    private bool frozen = false;

    /// <summary>
    /// Amount of nectar the agent has obtained in this episode
    /// </summary>
    public float nectarObtained { get; private set; }


    /// <summary>
    /// Initialize the agent
    /// </summary>
    public override void Initialize()
    {
        rigidbody = GetComponent<Rigidbody>();
        flowerArea = GetComponentInParent<FlowerArea>();
        // Assuming that the agent is a direct child of a GameObject that has FlowerArea script on it. Thats whats going to happen.
        // We have the island and the bird is going to be a direct child of it

        // If not training mode, no max step, play forever
        if (!trainingMode) MaxStep = 0;
    }

    /// <summary>
    /// Reset the agent when an episode begins
    /// </summary>
    public override void OnEpisodeBegin()
    {
        if (trainingMode)
        {
            // Only reset flowers in training when the is one agent per area
            flowerArea.ResetFlowers();
        }

        // Reset nectar obtained
        nectarObtained = 0f;

        // Zero out velocities so that movement stops before an episode begins
        rigidbody.velocity = Vector3.zero;
        rigidbody.angularVelocity = Vector3.zero;

        // Default to spawning in front of flower. This makes training easier to start
        bool inFrontOfFlower = true;
        if (trainingMode)
        {
            // Spawn in front of flower 50% of the time during training
            inFrontOfFlower = UnityEngine.Random.value > .5f;
        }

        // Move the agent to a new random position
        MoveToSafeRandomPosition(inFrontOfFlower);

        // Recalculate the nearest flower now after the agent is moved
        UpdateNearestFlower();
    }

    /// <summary>
    /// Called when an action is received either from the player input or from the neural network
    ///
    /// action.ContinuousActions[i] represents:
    /// Index 0: move vector x (+1 = right, -1 = left)
    /// Index 1: move vector y (+1 = up, -1 = down)
    /// Index 2: move vector z (+1 = forward, -1 = backward)
    /// Index 3: pitch angle (+1 = pitch up, -1 = pitch down)
    /// Index 4: yaw angle (+1 = turn right, -1 = turn left)
    /// </summary>
    /// <param name="actions">The actions to take</param>
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Dont take actions if frozen
        if (frozen) return;
        // This happens during Game, (might not be during training), we dont want the agent to take any action if frozen

        // Calculate movement vector
        Vector3 move = new Vector3(actions.ContinuousActions[0], actions.ContinuousActions[1], actions.ContinuousActions[2]);

        // Add force in the direction of move vector
        rigidbody.AddForce(move * moveForce);

        // Get the current rotation
        Vector3 rotationVector = transform.rotation.eulerAngles;

        // Calculate pitch and yaw rotation
        float pitchChange = actions.ContinuousActions[3];
        float yawChange = actions.ContinuousActions[4];

        // Calculate smooth rotation changes
        smoothPitchChange = Mathf.MoveTowards(smoothPitchChange, pitchChange, 2f * Time.fixedDeltaTime);
        smoothYawChange = Mathf.MoveTowards(smoothYawChange, yawChange, 2f * Time.fixedDeltaTime);

        // Calculate new pitch and yaw based on smoothed values
        // Clamp pitch to avaoid flipping upside down
        float pitch = rotationVector.x + (smoothPitchChange * Time.fixedDeltaTime * pitchSpeed);
        if (pitch > 180f) pitch -= 360f;
        pitch = Mathf.Clamp(pitch, -maxPitchAngle, maxPitchAngle);

        float yaw = rotationVector.y + (smoothYawChange * Time.fixedDeltaTime * yawSpeed);

        // Apply the new rotation
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    /// <summary>
    /// Collect vector observations from the environment
    /// </summary>
    /// <param name="sensor">Vector sensor</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        // If nearestFlower is null, observe an empty array and return early
        if (nearestFlower == null)
        {
            sensor.AddObservation(new float[10]);
            return;
        }

        // Observe the agent's local rotation (4 Observations)
        sensor.AddObservation(transform.localRotation.normalized);

        // Get a vector from the beak tip to the nearest flower
        Vector3 toFlower = nearestFlower.flowerCenterPosition - beakTip.position;

        // Observe a normalized vector pointing to the nearest flower (3 Observations)
        sensor.AddObservation(toFlower.normalized); // Normalized to a vector means its 1 meter long, i.e. it is purely a direction

        // Observe a dot product that indicates whether the beak tip is in front of the flower (1 Observation)
        // (+1 means that the beak tip is directly in front of the flower, -1 means directly behind)
        sensor.AddObservation(Vector3.Dot(toFlower.normalized, -nearestFlower.flowerUpVector.normalized));
        // i.e. If the direction of the vector from beak tip to the flower center aligns with the down vector of the flower,
        // then the dot is positive
        // That means the bird is in front of the flower

        // Observe a dot product that indicates whether the beak is pointing the flower (1 Observation)
        // (+1 means that the beak is pointing directly at the flower, -1 means directly away)
        sensor.AddObservation(Vector3.Dot(beakTip.forward.normalized, -nearestFlower.flowerUpVector.normalized));
        // i.e. If the direction of the vector from beak tip to the flower center is in same direction with the down vector of the flower,
        // then the dot is positive
        // That means the bird is pointing directly at the flower

        // Observe the relative distance from the beak tip to the flower (1 Observation)
        sensor.AddObservation(toFlower.magnitude / FlowerArea.areaDiameter);

        // 10 Total Observations

    }

    /// <summary>
    /// When Behavior Type is set to 'Heuristic Only' on the agent's Behavior Parameters,
    /// this function will be called. Its return values will be fed into
    /// <see cref="OnActionReceived(ActionBuffers)"/> instead of using the Neural Network
    /// </summary>
    /// <param name="actionsOut">Output Action array</param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActionsOut = actionsOut.ContinuousActions;

        // Create placeholders for all movement/turning
        Vector3 forward = Vector3.zero;
        Vector3 left = Vector3.zero;
        Vector3 up = Vector3.zero;
        float pitch = 0f;
        float yaw = 0f;

        // Convert keyboard inputs to movement and turning
        // All values should be between -1 and +1

        // transform.forward is relative to the HummingBird movement, it is different from Vector3.forward

        // Forward/Backward
        if (Input.GetKey(KeyCode.W)) forward = transform.forward;
        else if (Input.GetKey(KeyCode.S)) forward = -transform.forward;

        // Left/Right
        if (Input.GetKey(KeyCode.A)) left = -transform.right;
        else if (Input.GetKey(KeyCode.D)) left = transform.right;

        // Up/Down
        if (Input.GetKey(KeyCode.E)) up = transform.up;
        else if (Input.GetKey(KeyCode.C)) up = -transform.up;

        // Pitch Up/Down
        if (Input.GetKey(KeyCode.UpArrow)) pitch = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitch = -1f;

        // Yaw Left/Right
        if (Input.GetKey(KeyCode.LeftArrow)) yaw = -1f;
        else if (Input.GetKey(KeyCode.RightArrow)) yaw = 1f;

        // Combine the movement vector and normalize
        Vector3 combined = (forward + left + up).normalized;

        // Add the 3 movements values, pitch and yaw to the actionsOut array
        continuousActionsOut[0] = combined.x;
        continuousActionsOut[1] = combined.y;
        continuousActionsOut[2] = combined.z;
        continuousActionsOut[3] = pitch;
        continuousActionsOut[4] = yaw;

    }
    // We are creating a list of actions that will be passed into OnActionReceived() in any circumstance where wedont have a Neural Network hooked up

    /// <summary>
    /// Prevent the agent from moving and taking actions when we dont want it to
    /// </summary>
    public void FreezeAgent()
    {
        Debug.Assert(trainingMode, "Freeze/Unfreeze not supported in training");
        frozen = true;
        rigidbody.Sleep();
    }

    /// <summary>
    /// Resume agent movements and actions
    /// </summary>
    public void UnFreezeAgent()
    {
        Debug.Assert(trainingMode, "Freeze/Unfreeze not supported in training");
        frozen = false;
        rigidbody.WakeUp();
    }

    /// <summary>
    /// Move the agent to a safe position (i.e. Doesnt collide with anything)
    /// If in front of flower, also point the beak at the flower
    /// </summary>
    /// <param name="inFrontOfFlower">Whether to choose a spot in front of the flower</param>
    private void MoveToSafeRandomPosition(bool inFrontOfFlower)
    {
        bool safePositionFound = false;
        int attemptsRemaining = 100; // Prevent an infinite loop
        Vector3 potentialPosition = Vector3.zero;
        Quaternion potentialRotation = new Quaternion();

        // Loop until a safe position is found or we run out of attempts
        while (!safePositionFound && attemptsRemaining > 0)
        {
            attemptsRemaining--;
            if (inFrontOfFlower)
            {
                // Pick a random flower
                Flower randomFlower = flowerArea.flowers[UnityEngine.Random.Range(0, flowerArea.flowers.Count)];

                // Position 10 to 20 cm in front of the flower
                float distanceFromFlower = UnityEngine.Random.Range(.1f, .2f);
                potentialPosition = randomFlower.transform.position + randomFlower.flowerUpVector * distanceFromFlower;
                // flowerUpVector - Vector pointing straight out of the flower
                // This will spawn the bird in along the vector coming straight from the nectar

                // Point beak at flower (Bird's head is center of transform)
                Vector3 toFlower = randomFlower.flowerCenterPosition - potentialPosition;
                potentialRotation = Quaternion.LookRotation(toFlower, Vector3.up);
                // This just makes a rotation that will point the Bird's head directly at the flower


            }
            else
            {
                // Pick a random height from the ground
                float height = UnityEngine.Random.Range(1.2f, 2.5f);

                // Pick a random radius from the center of the area
                float radius = UnityEngine.Random.Range(2f, 7f);

                // Pick a random direction rotated about the y-axis
                Quaternion direction = Quaternion.Euler(0f, UnityEngine.Random.Range(-180f, 180f), 0f);

                // Combine height, radius and direction to pick a potential position
                potentialPosition = flowerArea.transform.position + (Vector3.up * height) + (direction * Vector3.forward * radius);

                // Choose and set random starting pitch and yaw
                float pitch = UnityEngine.Random.Range(-60f, 60f);
                float yaw = UnityEngine.Random.Range(-180f, 180f);
                potentialRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Check to see if the agent will collide with anything
            Collider[] colliders = Physics.OverlapSphere(potentialPosition, 0.05f); // Potential bubble radius around the bird to fit it in

            // Safe position has been found if no colliders overlapped
            safePositionFound = colliders.Length == 0;
        }
        Debug.Assert(safePositionFound, "Couldnt find a safe position to Spawn");

        // Set position and rotation
        transform.position = potentialPosition;
        transform.rotation = potentialRotation;
    }

    /// <summary>
    /// Undate the nearest flower to the agent
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    private void UpdateNearestFlower()
    {
        foreach (Flower flower in flowerArea.flowers)
        {
            if (nearestFlower == null && flower.hasNectar)
            {
                // No current nearest flower and current flower has nectar, so set to this flower
                nearestFlower = flower;
            }
            else if (flower.hasNectar)
            {
                // Calculate distance to this flower and Calculate distance to the current nearest flower
                float distanceToFlower = Vector3.Distance(flower.transform.position, beakTip.position);
                float distanceToCurrentNearestFlower = Vector3.Distance(nearestFlower.transform.position, beakTip.position);

                // If current nearest flower is empty OR this flower is closer, update the nearest flower
                if (!nearestFlower.hasNectar || distanceToFlower < distanceToCurrentNearestFlower)
                {
                    nearestFlower = flower;
                }
            }
        }
    }

    /// <summary>
    /// Called whent the agent's collider enters a trigger collider
    /// </summary>
    /// <param name="other">The trigger collider</param>
    private void OnTriggerEnter(Collider other)
    {
        TriggerEntryOrStay(other);
    }

    /// <summary>
    /// Called whent the agent's collider stays in a trigger collider
    /// </summary>
    /// <param name="other">The trigger collider</param>
    private void OnTriggerStay(Collider other)
    {
        TriggerEntryOrStay(other);
    }

    /// <summary>
    /// Handles when the agent's collider enters or stays in a trigger collider
    /// </summary>
    /// <param name="collider">The trigger collider</param>
    private void TriggerEntryOrStay(Collider collider)
    {
        // Check if agent is colliding with nectar
        if (collider.CompareTag("nectar")){
            Vector3 closestPointToBeakTip = collider.ClosestPoint(beakTip.position);

            // Check if the closest collision boint is close to the beak tip
            // Note: A collision with anything but the beak tip should not count
            if (Vector3.Distance(beakTip.position, closestPointToBeakTip) < beakTipRadius)
            {
                // Lookup the flower for this nectar collider
                Flower flower = flowerArea.GetFlowerFromNectar(collider);

                // Attempt to take 0.01 nectar
                // Note: This is per fixed time stamp, meaning this happens every 0.02 seconds or 50x per second
                float nectarReceived = flower.Feed(0.01f); // Takes 1% nectar every 0.02 seconds

                // Keep track of nectar obtained
                nectarObtained += nectarReceived;

                if (trainingMode)
                {
                    // Calculate reward for getting nectar
                    float bonus = .02f * Mathf.Clamp01(Vector3.Dot(transform.forward.normalized, -nearestFlower.flowerUpVector.normalized));
                    // This will encourage the agent to point its beak directly at the flower or into the flower
                    AddReward(.01f + bonus);
                    // This will always add a reward 0.01 if the agent's beak tip is inside the nectar, and adds a bonus if igt is pointing directly at it
                }

                // If the flower is empty, update the nearest flower
                if (!flower.hasNectar)
                {
                    UpdateNearestFlower();
                }
            }
        }
    }

    /// <summary>
    /// Called when the agent collides with something solid
    /// </summary>
    /// <param name="collision">The collision info</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (trainingMode && collision.collider.CompareTag("boundary")){
            // Collided with the area boundary, give a negative reward
            AddReward(-.5f);
        }
    }

    /// <summary>
    /// Called every frame
    /// </summary>
    private void Update()
    {
        // Draw a line to the beak tip to the nearest flower
        if (nearestFlower != null)
        {
            Debug.DrawLine(beakTip.position, nearestFlower.flowerCenterPosition, Color.grey);
            // This will show up only in the Scene Tab while the Game is playing
        }
    }

    /// <summary>
    /// Called every 0.02 seconds
    /// </summary>
    private void FixedUpdate()
    {
        // Avoids scenario where nearest flower nectar is stolen by opponent and not updated
        if (nearestFlower != null && !nearestFlower.hasNectar)
        {
            UpdateNearestFlower();
        }
    }
}
