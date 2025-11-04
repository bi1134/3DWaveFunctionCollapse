using UnityEngine;

public class Tile : MonoBehaviour
{
    [Header("Tiles that can be placed ABOVE this one (must connect DOWN to this)")]
    public Tile[] upNeighbors;

    [Header("Tiles that can be placed BELOW this one (must connect UP to this)")]
    public Tile[] downNeighbors;

    [Header("Tiles that can be placed to the LEFT (must connect RIGHT to this)")]
    public Tile[] leftNeighbors;

    [Header("Tiles that can be placed to the RIGHT (must connect LEFT to this)")]
    public Tile[] rightNeighbors;
}

