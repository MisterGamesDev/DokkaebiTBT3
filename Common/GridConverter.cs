using UnityEngine;
using Dokkaebi.Interfaces;
using Dokkaebi.Utilities;

namespace Dokkaebi.Common
{
    /// <summary>
    /// Handles basic conversions between different coordinate systems:
    /// - Grid positions (GridPosition)
    /// - World positions (Vector3)
    /// - Vector2Int positions
    /// </summary>
    public static class GridConverter
    {
        // Grid cell size in world units
        public static float CellSize = 1.0f;
        
        // Grid origin in world space
        public static Vector3 GridOrigin = Vector3.zero;
        
        // Default grid height in world space
        public static float DefaultGridHeight = 0f;

        /// <summary>
        /// Convert a grid position to a world position.
        /// </summary>
        public static Vector3 GridToWorld(GridPosition gridPosition, float height = -1f)
        {
            //SmartLogger.Log($"[GridConverter.GridToWorld] ENTRY - Input GridPosition: {gridPosition}, Height: {height}", LogCategory.Grid);
            //SmartLogger.Log($"[GridConverter.GridToWorld] Using Static Values - GridOrigin: {GridOrigin}, CellSize: {CellSize}", LogCategory.Grid);

            float worldX = GridOrigin.x + (gridPosition.x * CellSize) + (CellSize / 2f);
            float worldZ = GridOrigin.z + (gridPosition.z * CellSize) + (CellSize / 2f);
            float yPos = height >= 0 ? height : DefaultGridHeight;

            //SmartLogger.Log($"[GridConverter.GridToWorld] Calculated worldX: {worldX}, worldZ: {worldZ}", LogCategory.Grid);
            Vector3 result = new Vector3(worldX, yPos, worldZ);
            //SmartLogger.Log($"[GridConverter.GridToWorld] RETURN - Final World Position: {result}", LogCategory.Grid);
            return result;
        }

        /// <summary>
        /// Convert a world position to a grid position.
        /// </summary>
        public static GridPosition WorldToGrid(Vector3 worldPosition)
        {
            // Calculate grid coordinates relative to the origin
            float relativeX = worldPosition.x - GridOrigin.x;
            float relativeZ = worldPosition.z - GridOrigin.z;

            // Convert to grid coordinates using FloorToInt for consistency
            // (Clicking anywhere within a tile should map to that tile's index)
            int x = Mathf.FloorToInt(relativeX / CellSize);
            int z = Mathf.FloorToInt(relativeZ / CellSize);

            // Clamp to valid grid range (assuming gridWidth and gridHeight are accessible or defined constants)
            // You might need to fetch these values from GridManager if they are not static/const
            int gridWidth = 10;  // Assuming 10 based on your description
            int gridHeight = 10; // Assuming 10 based on your description
            x = Mathf.Clamp(x, 0, gridWidth - 1);
            z = Mathf.Clamp(z, 0, gridHeight - 1);

            return new GridPosition(x, z);
        }

        /// <summary>
        /// Convert a Vector2Int (X,Y) to a GridPosition (X,Z).
        /// Note: The Y component of Vector2Int becomes the Z component of GridPosition
        /// since we use the XZ plane for our grid.
        /// </summary>
        public static GridPosition Vector2IntToGrid(Vector2Int vector)
        {
            return new GridPosition(vector.x, vector.y);
        }

        /// <summary>
        /// Convert a GridPosition (X,Z) to a Vector2Int (X,Y).
        /// Note: The Z component of GridPosition becomes the Y component of Vector2Int
        /// since we use the XZ plane for our grid.
        /// </summary>
        public static Vector2Int GridToVector2Int(GridPosition gridPosition)
        {
            return new Vector2Int(gridPosition.x, gridPosition.z);
        }

        /// <summary>
        /// Gets the distance between two grid positions in grid units (Manhattan distance)
        /// </summary>
        public static int GetGridDistance(GridPosition a, GridPosition b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.z - b.z);
        }
    }
} 