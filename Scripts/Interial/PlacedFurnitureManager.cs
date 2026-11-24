using System.Collections.Generic;
using UnityEngine;

public class PlacedFurnitureManager : MonoBehaviour
{
    public static PlacedFurnitureManager Instance;

    public List<GameObject> placedFurnitures = new List<GameObject>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddFurniture(GameObject furnitureObject, FurnitureData data)
    {
        placedFurnitures.Add(furnitureObject);
        Debug.Log($"°¡±¸ Ãß°¡µÊ: {data.furnitureName}, ÃÑ {placedFurnitures.Count}°³");
    }

    public void RemoveFurniture(GameObject furnitureObject)
    {
        placedFurnitures.Remove(furnitureObject);
        Destroy(furnitureObject);
    }
}