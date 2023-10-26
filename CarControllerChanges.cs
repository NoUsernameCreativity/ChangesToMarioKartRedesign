using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class CarController : MonoBehaviour
{
    private Dictionary<String, float> MaterialNameVsCarColliderDrag = new Dictionary<String, float>() { 
                                                                                                      { "road", 0.5f },
                                                                                                      { "dirt", 2.5f }
                                                                                                      };
    Vector2[] currentAndNextCheckpointPositions = new Vector2[2] {Vector2.zero, Vector2.zero};
    private bool inputAllowed = true;

    void Update()
    {
        // get input
        float accel;
        float dir;
        if (inputAllowed)
        {
            accel = Input.GetAxis("Vertical");
            dir = Input.GetAxis("Horizontal");
        }
        else
        {
            accel = 0.0f;
            dir = 0.0f;
        }

        // get details about ground material
        RaycastHit hit;
        Physics.Raycast(carCollider.position, Vector3.down, out hit);
        Material mat = GetGroundMaterial(hit);
        if (mat != null) {
            if (MaterialNameVsCarColliderDrag.ContainsKey(mat.name))
            {
                carBody.drag = MaterialNameVsCarColliderDrag[mat.name];
            }
        }
        if(boost > 0.0f)
        {
            carBody.drag = 0.0f;
        }
    }

    public float GetDecimalPercentToNextCheckpoint()
    {
        Vector2 displacementToPlayer = new Vector2(carBody.transform.position.x, carBody.transform.position.z) - currentAndNextCheckpointPositions[0];
        Vector2 displacementToNextCheckpoint = currentAndNextCheckpointPositions[1] - currentAndNextCheckpointPositions[0];

        // calculate scalar resolute (length of projection)
        float dot = Vector2.Dot(displacementToPlayer, displacementToNextCheckpoint);
        return dot / (displacementToNextCheckpoint.magnitude * displacementToNextCheckpoint.magnitude);
    }

    void RepositionPlaceInRace()
    {
        // if ahead of the person above in the script heirarchy, replace them
        int currentPosition = transform.GetSiblingIndex();
        if (currentPosition > 0) // not in first
        {
            Transform playerAhead = transform.parent.GetChild(currentPosition - 1);
            if (playerAhead.tag == "Player")
            {
                CarController script = playerAhead.GetComponent<CarController>();
                float[] playerProgress = new float[3] {GetCompletedLaps(), GetCollectedCheckpoints(), GetDecimalPercentToNextCheckpoint()};
                float[] comparisonProgress = new float[3] { script.GetCompletedLaps(), script.GetCollectedCheckpoints(), script.GetDecimalPercentToNextCheckpoint() };
                for(int i=0; i<3; i++)
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
