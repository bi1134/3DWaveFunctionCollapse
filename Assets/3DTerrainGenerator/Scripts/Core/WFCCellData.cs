using System.Collections.Generic;
using UnityEngine;

namespace TerrainGenerator
{
    /// <summary>
    /// Lightweight data-only cell for Dual Grid layers.
    /// No GameObject instantiation required.
    /// Implements IWFCCell for compatibility with WFC algorithm.
    /// </summary>
    [System.Serializable]
    public class WFCCellData : IWFCCell
    {
        private Vector3Int _gridPosition;
        private bool _collapsed = false;
        private List<WFCModule> _possibleModules = new List<WFCModule>();
        private int _totalGridHeight = 1;
        private float _heightRatio = 0;

        // IWFCCell interface implementation
        public Vector3Int GridPosition => _gridPosition;
        public bool Collapsed { get => _collapsed; set => _collapsed = value; }
        public List<WFCModule> PossibleModules => _possibleModules;
        public int Entropy => _possibleModules.Count;
        public float HeightRatio => _heightRatio;
        public int TotalGridHeight => _totalGridHeight;

        public void Initialize(List<WFCModule> allModules, Vector3Int pos, int gridHeight)
        {
            _possibleModules = new List<WFCModule>(allModules);
            _gridPosition = pos;
            _totalGridHeight = gridHeight;
            _heightRatio = (gridHeight > 1) ? (float)pos.y / ((float)gridHeight - 1) : 0;
            _collapsed = false;
        }

        public void Collapse()
        {
            if (_possibleModules.Count == 0)
            {
                Debug.LogError($"CellData at {_gridPosition} has 0 entropy! Collapse failed.");
                return;
            }

            WFCModule selected = SelectModuleWeighted();
            _possibleModules.Clear();
            _possibleModules.Add(selected);
            _collapsed = true;
        }

        public bool Constrain(List<WFCModule> allowedModules)
        {
            if (_collapsed) return false;

            int before = _possibleModules.Count;
            _possibleModules.RemoveAll(m => !allowedModules.Contains(m));
            return _possibleModules.Count < before;
        }

        private WFCModule SelectModuleWeighted()
        {
            float totalWeight = 0;
            foreach (var m in _possibleModules)
                totalWeight += m.spawnWeight;

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0;

            foreach (var m in _possibleModules)
            {
                cumulative += m.spawnWeight;
                if (roll <= cumulative) return m;
            }

            return _possibleModules[_possibleModules.Count - 1];
        }
    }
}
