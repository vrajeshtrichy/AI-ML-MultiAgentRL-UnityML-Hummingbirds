using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a collection of flower plants and attached flowers
/// </summary>
public class FlowerArea : MonoBehaviour
{
    // Diameter of the area where the Agent and Flowers and be used
    // for observing relative distance from the agent to the flower
    public const float areaDiameter = 20f;

    // List of all flower plants in this flower area
    // (flower plants have multiple flowers)
    private List<GameObject> flowerPlants;

    // Lookup dictionary for looking up a flower from a nector collider Key
    private Dictionary<Collider, Flower> nectarFlowerDictionary;

    /// <summary>
    /// List of all flowers in the flower area
    /// </summary>
    public List<Flower> flowers { get; private set; }

    /// <summary>
    /// Reset the flowers and flower plants
    /// </summary>
    public void ResetFlowers()
    {
        // Rotate each flower plant around the Y-Axix and subtly around the X and Z
        foreach (GameObject flowerPlant in flowerPlants)
        {
            float xRotation = UnityEngine.Random.Range(-5f, 5f);
            float yRotation = UnityEngine.Random.Range(-180f, 180f);
            float zRotation = UnityEngine.Random.Range(-5f, 5f);

            flowerPlant.transform.localRotation = Quaternion.Euler(xRotation, yRotation, zRotation);
        }

        // Reset each flower
        foreach (Flower flower in flowers)
        {
            flower.ResetFlower();
        }
    }

    /// <summary>
    /// Gets the <see cref="Flower"/> the nectar collider belongs to
    /// </summary>
    /// <param name="collider">Nectar Collider</param>
    /// <returns>Matching Flower</returns>
    public Flower GetFlowerFromNectar(Collider collider)
    {
        return nectarFlowerDictionary[collider];
    }

    /// <summary>
    /// Called when the Area wakes up
    /// </summary>
    private void Awake()
    {
        // Initialize variables
        flowerPlants = new List<GameObject>();
        nectarFlowerDictionary = new Dictionary<Collider, Flower>();
        flowers = new List<Flower>();
    }

    /// <summary>
    /// Called when then Game Starts
    /// </summary>
    private void Start()
    {
        // Find all flowers that are children of this GameObject/Transform
        FindChildFlowers(transform);
    }

    // This Function helps to automatically get all the flower buds in the area

    /// <summary>
    /// Recursively finds all flowers and flower plants that are children of a parent transform
    /// </summary>
    /// <param name="parent">Parent of the children to check</param>
    private void FindChildFlowers(Transform parent)
    {
        for(int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if(child.CompareTag("flower_plant"))
            {
                // Found a flower plant -> Add it to the flower plants list
                flowerPlants.Add(child.gameObject);

                // Look for flowers within the flowerplant
                FindChildFlowers(child);
            }
            else
            {
                // Not a flower plant -> Look for a flower component
                // If it is not a flower plant, it can be anything else or a Flower
                Flower flower = child.GetComponent<Flower>();
                if (flower != null)
                {
                    // Found a flower -> Add it to the Flowers list
                    flowers.Add(flower);

                    // Add the Nectar collider to the Lookup Dictionary
                    nectarFlowerDictionary.Add(flower.nectarCollider, flower);

                    // Note: There are no flowers that are children of other flowers
                }
                else
                {
                    // Flower component not found, so check for children
                    FindChildFlowers(child);
                }
            }
        }
    }
}
