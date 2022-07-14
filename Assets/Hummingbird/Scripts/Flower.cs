using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a Single Flower with Nectar
/// </summary>s
public class Flower : MonoBehaviour
{
    [Tooltip("Color when the flower is full")]
    public Color fullFlowerColor = new Color(1f, 0f, 0.3f);

    [Tooltip("Color when the flower is empty")]
    public Color emptyFlowerColor = new Color(0.5f, 0f, 1f);

    /// <summary>
    /// Trigger representing the nectar
    /// </summary>
    [HideInInspector]
    public Collider nectarCollider;

    // Solid collider representing the flower
    private Collider flowerCollider;

    // Flower's Material
    private Material flowerMaterial;

    /// <summary>
    /// Vector pointing straight out of the flower
    /// </summary>
    public Vector3 flowerUpVector
    {
        get
        {
            return nectarCollider.transform.up;
        }
    }

    /// <summary>
    /// Center position of nector collider
    /// </summary>
    public Vector3 flowerCenterPosition
    {
        get
        {
            return nectarCollider.transform.position;
        }
    }

    /// <summary>
    /// Amount of nector remaining in the flower
    /// </summary>
    public float nectarAmount
    {
        get;
        private set;
    }

    /// <summary>
    /// Whether the flower has any nectar remaining
    /// </summary>
    public bool hasNectar
    {
        get
        {
            return nectarAmount > 0f;
        }
    }

    /// <summary>
    /// Attempts to remove nector from the flower
    /// </summary>
    /// <param name="amount">Amount of nector to remove</param>
    /// <returns>Actual amount of nector removed</returns>
    public float Feed(float amount)
    {
        // Track how much nectar was successfully taken (cannot take more than available)
        float nectarTaken = Mathf.Clamp(amount, 0f, nectarAmount);

        // Subtract the nectar
        nectarAmount -= amount;

        if(nectarAmount <= 0)
        {
            // No nectar remaining
            nectarAmount = 0;

            // Disable Nectar and Flower Colliders
            nectarCollider.gameObject.SetActive(false);
            flowerCollider.gameObject.SetActive(false);

            // Change the flower color to indicate that its empty
            flowerMaterial.SetColor("_BaseColor", emptyFlowerColor);
        }

        // Return the amount of nectar that was taken
        return nectarTaken;
    }

    /// <summary>
    /// Resets the flower
    /// </summary>
    public void ResetFlower()
    {
        // Refill Nectar
        nectarAmount = 1f;

        // Enable Nectar and Flower Colliders
        nectarCollider.gameObject.SetActive(true);
        flowerCollider.gameObject.SetActive(true);

        // Change the flower color to indicate that its full
        flowerMaterial.SetColor("_BaseColor", fullFlowerColor);
    }

    /// <summary>
    /// Called when the flower wakes up
    /// </summary>
    private void Awake()
    {
        // Find the flowers's mesh renderer and get the main material
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        flowerMaterial = meshRenderer.material;

        // Find flower and nectar colliders
        flowerCollider = transform.Find("FlowerCollider").GetComponent<Collider>();
        nectarCollider = transform.Find("FlowerNectarCollider").GetComponent<Collider>();
    }
}
