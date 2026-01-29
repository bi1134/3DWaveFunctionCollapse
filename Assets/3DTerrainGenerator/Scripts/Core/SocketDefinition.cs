using UnityEngine;

public enum SocketID
{
    Air,         // 0 - Nothing
    Wall,   // Basic Wall Connector
    WallTopThin,     // Vertical connector for Walls
    WallThinHorizontal,   // Horizontal connector for Walls
    WallThinVertical,
    WallThinFourWay,
    WallThinCornerA,
    WallThinCornerB,
    WallThinCornerC,
    WallThinCornerD,
    WallTopThick,
    WallThickHorizontal,
    WallThickVertical,
    WallThickFourWay,
    WallThickCornerA,
    WallThickCornerB,
    WallThickCornerC,
    WallThickCornerD,
    Roof,        // Roof edge or slope
    RoofVertical,        
    RoofHorizontal,
    Ground,      // Floor/Base connector
    Window,       // Optional specific connector
}
