using UnityEngine;

public class ModularTile : MonoBehaviour
{
    [Header("Tile Identifier")]

    [Tooltip("Optional name override. If empty, uses the prefab's GameObject name.")]
    public string tileName;


    [Header("Socket Rules")]
    [Tooltip("Top face (Y+). Use 'v' prefix for vertical sockets. Example: v0_0")]
    public string top;

    [Tooltip("Bottom face (Y-). Use 'v' prefix for vertical sockets. Example: v0_0")]
    public string bottom;

    [Tooltip("Front face (Z+).")]
    public string front;

    [Tooltip("Back face (Z-).")]
    public string back;

    [Tooltip("Right face (X+).")]
    public string right;

    [Tooltip("Left face (X-).")]
    public string left;

    /// <summary>
    /// Returns a display-friendly name.
    /// </summary>
    public string GetName()
    {
        return string.IsNullOrEmpty(tileName) ? gameObject.name : tileName;
    }
}
