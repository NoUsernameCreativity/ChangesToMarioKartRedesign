using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class AICarController : MonoBehaviour
{
    // Game Loop information (may need to make public)
    private int LapsCompleted;
    private List<string> CheckpointsCollected = new List<string>();

    // AI Control Vars
    public Vector2 targetPositionVariation = new Vector2(0, 0);
    public float speedTargetVariation = 0.0f;
    public Vector2 StartEndDriftingAngle = new Vector2(20, 5);
    private AiControls controls = new AiControls();
    private Vector3 targetPosition;
    public float steerAmountMultiplier = 1.5f;
    public float targetSpeed = 15.0f;
    private Vector2 wallReverseEnterExit = new Vector2(1, 3);
    private bool reversingFromWall = false;

    // tuning
    public float turnSpeed = 1f;
    public float turnSmoothing = 8f;
    public float slopeRotationSmoothing = 6f;
    public float airRotationSmoothing = 2f;
    public float accelSpeed = 12f;
    public float sidewaysWheelDrag = 1f;
    public float driftTurnAmount = 1000.0f;
    public float driftTurnSpeed = 6.0f;
    public Vector3[] boostTimingPowerandDuration = { new Vector3(1.0f, 1, 1), new Vector3(3.0f, 1.2f, 2), new Vector3(6.0f, 1.4f, 3) };
    public Transform[] wheelMeshes;
    private Dictionary<String, float> MaterialNameVsCarColliderDrag = new Dictionary<String, float>() {
                                                                                                      { "road", 0.5f },
                                                                                                      { "dirt", 2.5f }
                                                                                                      };

    // visuals
    public Vector3 carMeshPositionAdjustment = new Vector3();
    const float groundDistance = 0.45f; // distance the ground is away to be considered ground
    const float wheelVisualSteerAmount = 250f;

    // necessary stuff
    public Transform carMesh;
    public Transform carCollider;
    public Rigidbody carBody; // initialised by getting rigidbody component in start
    public Transform wheelPosition;

    // private vars
    float horizontalDirection;
    float steeringDirection;
    float targetSteeringDirection;
    float driftDirection;
    float targetDriftDirection;
    float timeDrifting;
    float boost;
    bool isKartDrifting;
    bool isGrounded;
    Vector3 respawnPosition;
    float respawnRotation;
    Vector2[] currentAndNextCheckpointPositions = new Vector2[2] { Vector2.zero, Vector2.zero };
    bool inputAllowed = true;

    public class AiControls
    {
        // axis between 0 and 1
        public float verticalAxis;
        public float horizontalAxis;
        // bool
        public bool driftKeyPressed;

        public AiControls()
        {
            verticalAxis = 0.0f;
            horizontalAxis = 0.0f;
            driftKeyPressed = false;
        }
    }

    private void ControlAiInput()
    {
        // control acceleration
        controls.verticalAxis = 0.0f;
        if (carBody.velocity.magnitude > targetSpeed)
        {
            controls.verticalAxis = -1.0f;
        }
        else if (carBody.velocity.magnitude < targetSpeed)
        {
            controls.verticalAxis = 1.0f;
        }
        // control steering

        // get displacements
        Vector3 displacementToTarget = targetPosition - carCollider.transform.position;
        Vector2 displacement2DToTarget = new Vector2(displacementToTarget.x, displacementToTarget.z);
        Vector2 forwardVector2D = new Vector2(carMesh.forward.x, carMesh.forward.z);

        // normalise (so that the dot product stays between 0 and 1, as dot(a,b) = |a||b|cos(θ))
        displacement2DToTarget = displacement2DToTarget.normalized;
        forwardVector2D = forwardVector2D.normalized;

        // find angle (in radians) between desired angle (to target) and actual angle. Note that the clamp is necessary due to floating point precision errors
        float dirBetweenVectors = Mathf.Acos(Mathf.Clamp(Vector2.Dot(forwardVector2D, displacement2DToTarget), -1.0f, 1.0f));

        // correct angle depending on whether the target is to the right or left
        float crossProduct = (forwardVector2D.x * displacement2DToTarget.y) - (displacement2DToTarget.x * forwardVector2D.y);
        if (crossProduct > 0.0f)
        {
            dirBetweenVectors = -dirBetweenVectors;
        }

        // use that angle to steer
        controls.horizontalAxis = Mathf.Clamp(dirBetweenVectors*steerAmountMultiplier, -1.0f, 1.0f);

        // drift if the angle is very large
        if (controls.driftKeyPressed)
        {
            if (Mathf.Abs(dirBetweenVectors) < StartEndDriftingAngle.y * Mathf.PI / 180.0f)
            {
                controls.driftKeyPressed = false;
            }
        }
        else {
            if (Mathf.Abs(dirBetweenVectors) > StartEndDriftingAngle.x * Mathf.PI / 180.0f)
            {
                controls.driftKeyPressed = true;
            }
        }

        // deal with walls
        RaycastHit hit = new RaycastHit();
        Physics.Raycast(carCollider.position, carMesh.forward, out hit);
        if (reversingFromWall)
        {
            controls.verticalAxis = -1;
            if(hit.transform.tag != "Wall" || hit.distance > wallReverseEnterExit.y)
            {
                reversingFromWall = false;
            }
        }
        {
            if (hit.transform.tag == "Wall" && hit.distance < wallReverseEnterExit.x)
            {
                reversingFromWall = true;
            }
        }
    }






    // REST IS THE SAME AS THE CAR CONTROLLER





    public int GetCompletedLaps()
    {
        return LapsCompleted;
    }

    public int GetCollectedCheckpoints()
    {
        return CheckpointsCollected.Count;
    }

    public float GetDecimalPercentToNextCheckpoint()
    {
        Vector2 displacementToPlayer = new Vector2(carBody.transform.position.x, carBody.transform.position.z) - currentAndNextCheckpointPositions[0];
        Vector2 displacementToNextCheckpoint = currentAndNextCheckpointPositions[1] - currentAndNextCheckpointPositions[0];
        // calculate scalar resolute (length of projection)
        float dot = Vector2.Dot(displacementToPlayer, displacementToNextCheckpoint);
        //Debug.Log(displacementToPlayer);
        return dot / (displacementToNextCheckpoint.magnitude * displacementToNextCheckpoint.magnitude);
    }

    public void FinishLap(Vector3 position, Vector3 newTargetPosition, float newTargetSpeed, float newSteerAmountMultiplier, Vector3 nextCheckpointPosition)
    {
        targetPosition = newTargetPosition;
        steerAmountMultiplier = newSteerAmountMultiplier;
        targetSpeed = newTargetSpeed;
        if (CheckpointsCollected.Count >= GameInformation.CheckpointsNeeded)
        {
            Debug.Log("Lap Finished!");
            LapsCompleted++;
            CheckpointsCollected = new List<string>();
            respawnPosition = position;
            respawnRotation = horizontalDirection;
        }
        if (LapsCompleted >= GameInformation.LapsNeeded)
        {
            Debug.Log("Game Complete");
            // finish game
            SceneManager.LoadScene("GameOverScreen");
        }
        currentAndNextCheckpointPositions[0] = new Vector2(position.x, position.z);
        currentAndNextCheckpointPositions[1] = new Vector2(nextCheckpointPosition.x, nextCheckpointPosition.z);
    }

    public void CollectCheckpoint(string checkpointName, Vector3 position, Vector3 newTargetPosition, float newTargetSpeed, float newSteerAmountMultiplier, Vector3 nextCheckpointPosition)
    {
        targetPosition = newTargetPosition + new Vector3(UnityEngine.Random.Range(-targetPositionVariation.x, targetPositionVariation.x), 0.0f, UnityEngine.Random.Range(-targetPositionVariation.y, targetPositionVariation.y));
        steerAmountMultiplier = newSteerAmountMultiplier;
        targetSpeed = newTargetSpeed + UnityEngine.Random.Range(-speedTargetVariation, speedTargetVariation);
        if (!CheckpointsCollected.Contains(checkpointName))
        {
            CheckpointsCollected.Add(checkpointName);
            //Debug.Log("AI: " + gameObject.name + " Checkpoints collected: " + (CheckpointsCollected.Count).ToString() + " / " + GameInformation.CheckpointsNeeded.ToString());
            respawnPosition = position;
            respawnRotation = horizontalDirection;
        }
        currentAndNextCheckpointPositions[0] = new Vector2(position.x, position.z);
        currentAndNextCheckpointPositions[1] = new Vector2(nextCheckpointPosition.x, nextCheckpointPosition.z);
    }

    public void Respawn()
    {
        carBody.transform.position = respawnPosition + new Vector3(0, 1, 0);
        horizontalDirection = respawnRotation;
        carBody.velocity = Vector3.zero;
        carBody.angularVelocity = Vector3.zero;
    }

    // Start is called before the first frame update
    void Start()
    {
        carBody = carCollider.GetComponent<Rigidbody>();
        horizontalDirection = transform.rotation.eulerAngles.y;
        respawnPosition = carCollider.transform.position;
        respawnRotation = horizontalDirection;
    }

    // Update is called once per frame
    void Update()
    {
        // get input
        if (inputAllowed)
        {
            ControlAiInput();
        }
        else
        {
            controls = new AiControls(); // no buttons
        }
        float accel = controls.verticalAxis;
        float dir = controls.horizontalAxis;

        // set position of mesh to the position of the sphere collider
        carMesh.transform.position = carCollider.transform.position + carMeshPositionAdjustment;

        Steering(dir);
        Drifting();
        RepositionPlaceInRace();
        // rotate the mesh onto the slope
        RaycastHit hit;
        Physics.Raycast(carCollider.position, Vector3.down, out hit);
        RotateCarMesh(hit);
        Material mat = GetGroundMaterial(hit);
        if (mat != null)
        {
            if (MaterialNameVsCarColliderDrag.ContainsKey(mat.name))
            {
                carBody.drag = MaterialNameVsCarColliderDrag[mat.name];
            }
        }
        if (boost > 0.0f)
        {
            //carBody.drag = 0.0f; // breaks AI
        }
        // target debug
        Debug.DrawLine(carCollider.transform.position, targetPosition, Color.red);
        Debug.DrawLine(carCollider.transform.position, carCollider.transform.position + carMesh.forward * 3.0f, Color.red);
        Debug.DrawLine(carCollider.transform.position, carCollider.transform.position + carMesh.right * 5.0f * controls.horizontalAxis, Color.green);
    }

    void FixedUpdate()
    {
        float accel = controls.verticalAxis;
        // apply forward acceleration forces to the car
        if (isGrounded)
        {
            carBody.AddForce(carMesh.forward * (accel + boost) * accelSpeed);
        }
        // apply drag force
        if (!controls.driftKeyPressed)
        {
            float drag = Mathf.Abs(Vector3.Dot(carBody.velocity.normalized, carMesh.transform.right));
            drag = Mathf.Pow(drag, 2);
            carBody.AddForce(sidewaysWheelDrag * drag * -carBody.velocity.normalized);
        }

        ApproximateTurning();
    }

    void Steering(float inputDir)
    {
        // steering
        targetSteeringDirection = inputDir * turnSpeed;
        steeringDirection = Mathf.Lerp(steeringDirection, targetSteeringDirection, turnSmoothing * Time.deltaTime);
        //// used to fix a bug where the steering direction was becoming NaN, causing a problem
        //if (float.IsNaN(steeringDirection))
        //{
        //    steeringDirection = targetSteeringDirection;
        //}
    }

    void Drifting()
    {
        // drifting
        if (controls.driftKeyPressed)
        {
            targetDriftDirection = targetSteeringDirection * driftTurnAmount;
        }
        else
        {
            targetDriftDirection = 0.0f;
        }
        driftDirection = Mathf.Lerp(driftDirection, targetDriftDirection, driftTurnSpeed * Time.deltaTime);

        // deal with initiating and finishing drifts
        if (isKartDrifting)
        {
            timeDrifting += Time.deltaTime;
            // cancel drift (cancelled in midair, if steering is stopped, if shift is pressed)
            if (Input.GetKeyUp(KeyCode.LeftShift) || targetDriftDirection == 0 || !isGrounded)
            {
                // cancel drift
                isKartDrifting = false;
                //Debug.Log("Time spent in drift: " + timeDrifting.ToString());
                // initiate boost
                Vector2 boostingInformation = CalculateBoost(timeDrifting);
                if (boostingInformation.y != 0.0f)
                {
                    boost = boostingInformation.x;
                    // cancel boost after 'duration' seconds
                    Invoke("CancelBoost", boostingInformation.y);
                }
                timeDrifting = 0.0f;
            }
        }
        else
        {
            if (controls.driftKeyPressed && targetDriftDirection != 0)
            {
                isKartDrifting = true;
            }
        }
    }

    void CancelBoost()
    {
        boost = 0.0f;
    }

    // returns vec2(boost power, boost duration)
    Vector2 CalculateBoost(float timeSpentDrifting)
    {
        // Loop through list of boost timings, to see what boost the player got
        for (int i = boostTimingPowerandDuration.Length - 1; i >= 0; i--)
        {
            if (boostTimingPowerandDuration[i].x < timeSpentDrifting)
            {
                return new Vector2(boostTimingPowerandDuration[i].y, boostTimingPowerandDuration[i].z);
            }
        }
        // drift was not held long enough for a boost
        return new Vector2(0, 0);
    }

    void ApproximateTurning()
    {
        // turn the car mesh
        // get speed to know how much the car should turn
        float speed = carBody.velocity.magnitude;

        // pretend wheels to turn the car semi-accurately
        Vector2 frontWheelsPosition = new Vector2(0, wheelPosition.localPosition.z);
        frontWheelsPosition += speed * new Vector2(Mathf.Sin(steeringDirection), Mathf.Cos(steeringDirection));
        Vector2 backWheelsPosition = new Vector2(0, -wheelPosition.localPosition.z);
        backWheelsPosition += new Vector2(0, speed);

        // get the angle between the wheels and use that to change the rotation of the car
        Vector2 displacement = frontWheelsPosition - backWheelsPosition;
        float angleBetweenWheels = Mathf.Atan2(displacement.x, displacement.y);
        horizontalDirection += angleBetweenWheels;
    }

    private Material GetGroundMaterial(RaycastHit hit)
    {
        if (isGrounded)
        {
            MeshCollider collider = hit.collider as MeshCollider;
            if (collider != null && collider.sharedMesh != null) // check no null
            {
                Mesh mesh = collider.sharedMesh;

                // There are 3 indices stored per triangle, GetTriangles() returns these indices
                int countedVertices = 0;
                // get submesh the triangle index is in
                int submesh;
                for (submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    int numIndices = mesh.GetTriangles(submesh).Length;
                    countedVertices += numIndices;
                    if (countedVertices > hit.triangleIndex * 3) // found the submesh
                        break;
                }

                // Get corresponding material to submesh
                Material material = collider.GetComponent<MeshRenderer>().sharedMaterials[submesh];
                return material;
            }
        }
        return null;
    }
    void RotateCarMesh(RaycastHit hit)
    {
        if (hit.distance < groundDistance)
        {
            isGrounded = true;
            carMesh.transform.up = Vector3.Lerp(carMesh.transform.up, hit.normal, slopeRotationSmoothing * Time.deltaTime);
        }
        else
        {
            isGrounded = false;
            carMesh.transform.up = Vector3.Lerp(carMesh.transform.up, Vector3.up, airRotationSmoothing * Time.deltaTime);
        }

        // rotate car
        carMesh.transform.rotation *= Quaternion.Euler(Vector3.up * (horizontalDirection + driftDirection));

        // controls the way that drifting tilts wheels the opposite way
        float effectOfDriftOnWheelRotation = 0.01f;
        // rotate wheels
        foreach (Transform wheel in wheelMeshes)
        {
            wheel.localRotation = Quaternion.Euler(Vector3.up * (steeringDirection - driftDirection * effectOfDriftOnWheelRotation) * wheelVisualSteerAmount);
        }
    }

    void RepositionPlaceInRace()
    {
        // if ahead of the person above in the script heirarchy, replace them
        int currentPosition = transform.GetSiblingIndex();
        // in first
        if (currentPosition > 0)
        {
            Transform playerAhead = transform.parent.GetChild(currentPosition - 1);
            if (playerAhead.tag == "Player")
            {
                CarController script = playerAhead.GetComponent<CarController>();
                float[] playerProgress = new float[3] { GetCompletedLaps(), GetCollectedCheckpoints(), GetDecimalPercentToNextCheckpoint() };
                float[] comparisonProgress = new float[3] { script.GetCompletedLaps(), script.GetCollectedCheckpoints(), script.GetDecimalPercentToNextCheckpoint() };
                for (int i = 0; i < 3; i++)
                {
                    if (playerProgress[i] > comparisonProgress[i])
                    {
                        transform.SetSiblingIndex(currentPosition - 1);
                        break; // ahead of next player
                    }
                    else if (playerProgress[i] < comparisonProgress[i])
                    {
                        break; // behind next player
                    }
                }
            }
            else
            {
                AICarController script = playerAhead.GetComponent<AICarController>();
                float[] playerProgress = new float[3] { GetCompletedLaps(), GetCollectedCheckpoints(), GetDecimalPercentToNextCheckpoint() };
                float[] comparisonProgress = new float[3] { script.GetCompletedLaps(), script.GetCollectedCheckpoints(), script.GetDecimalPercentToNextCheckpoint() };
                for (int i = 0; i < 3; i++)
                {
                    if (playerProgress[i] > comparisonProgress[i])
                    {
                        transform.SetSiblingIndex(currentPosition - 1);
                        break; // ahead of next player
                    }
                    else if (playerProgress[i] < comparisonProgress[i])
                    {
                        break; // behind next player
                    }
                }
            }
        }
    }

    public void DealWithShellCollision(float timeInputDeactivated, Vector3 forceOfHit)
    {
        inputAllowed = false;
        carBody.AddForce(forceOfHit, ForceMode.Impulse);
        Invoke("ReEnableInput", timeInputDeactivated);
    }

    void ReEnableInput()
    {
        inputAllowed = true;
    }
}
