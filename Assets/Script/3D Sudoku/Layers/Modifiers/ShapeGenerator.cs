using UnityEngine;
using System.Collections.Generic;

namespace WFC_Sudoku
{
    [System.Serializable]
    public class ShapeGenerator : WFCModifier
    {
        public enum ShapeType { Rectangle, Circle }
        public ShapeType shapeType;
        
        [Header("Rectangle Settings")]
        public Vector2Int rectSize = new Vector2Int(10, 10);
        public Vector2Int centerOffset = Vector2Int.zero; // Relative to center

        [Header("Circle Settings")]
        public float radius = 5f;
        
        [Header("Common")]
        public bool fill = true;
        public int borderWidth = 1;

        public override void Apply(WFCBlueprintLayer layer, List<WFCBlueprintLayer> context)
        {
            Texture2D map = layer.outputMap;
            int w = map.width;
            int h = map.height;
            
            // Clear or Add? usually Generators clear, but as a modifier stack, maybe we ADD to existing?
            // Let's assume ADD. If they want clear, they can use an empty start.
            // Actually, "Invert" etc assumes there is something.
            // Let's just draw on top.
            
            int centerX = w / 2 + centerOffset.x;
            int centerY = h / 2 + centerOffset.y;

            if (shapeType == ShapeType.Rectangle)
            {
                int startX = centerX - rectSize.x / 2;
                int startY = centerY - rectSize.y / 2;
                int endX = startX + rectSize.x;
                int endY = startY + rectSize.y;
                
                for(int x = 0; x < w; x++)
                {
                    for(int y=0; y < h; y++)
                    {
                        bool inside = (x >= startX && x < endX && y >= startY && y < endY);
                        if (!fill)
                        {
                            // Border logic
                            bool border = (x >= startX && x < startX + borderWidth) ||
                                          (x >= endX - borderWidth && x < endX) ||
                                          (y >= startY && y < startY + borderWidth) ||
                                          (y >= endY - borderWidth && y < endY);
                            if (inside && !border) inside = false;
                        }
                        
                        if (inside) map.SetPixel(x, y, layer.activeColor);
                    }
                }
            }
            else if (shapeType == ShapeType.Circle)
            {
                float rSq = radius * radius;
                float innerRSq = (radius - borderWidth) * (radius - borderWidth);
                
                for(int x = 0; x < w; x++)
                {
                    for(int y=0; y < h; y++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float distSq = dx*dx + dy*dy;
                        
                        bool inside = distSq <= rSq;
                        if (!fill && inside)
                        {
                             if (distSq < innerRSq) inside = false;
                        }

                        if (inside) map.SetPixel(x, y, layer.activeColor);
                    }
                }
            }
            map.Apply();
        }
    }
}
