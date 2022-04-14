using System.Linq;
using UnityEngine;

namespace Bubbles.Scripts
{
    public class Tile : Element
    {
        private Material _bodyMaterial;
        private Material _indicatorMaterial;

        public Color TileColor
        {
            get => _indicatorMaterial.color;
            set => _indicatorMaterial.color = value;
        }
        
        void Awake()
        {
            Material[] materials = GetComponentInChildren<Renderer>().materials;
            _bodyMaterial = materials.First(mat => mat.name.Contains("Body"));
            _indicatorMaterial = materials.First(mat => mat.name.Contains("Indicator"));
            
            if (_indicatorMaterial != null && _bodyMaterial != null)
            {
                _indicatorMaterial.color = _bodyMaterial.color;
            }
        }

        public void StopIndicating()
        {
            if (_indicatorMaterial != null && _bodyMaterial != null)
            {
                _indicatorMaterial.color = _bodyMaterial.color;
            }
        }

        public override void OnClick()
        {
            parentBoard.OnTileClicked(boardPosition);
        }
    }
}