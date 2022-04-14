using System.Linq;
using UnityEngine;

namespace Bubbles.Scripts
{
    public class Bubble : BoardElement
    {
        private Material _topMaterial;

        public Color BubbleColor
        {
            get => _topMaterial.color;
            set => _topMaterial.color = value;
        }
        
        void Awake()
        {
            Material[] materials = GetComponentInChildren<Renderer>().materials;
            _topMaterial = materials.First(mat => mat.name.Contains("Top"));
        }

        public void SetColor(Color newColor)
        {
            if (_topMaterial != null)
            {
                _topMaterial.color = newColor;
            }
        }

        public override void OnClick()
        {
            parentBoard.OnBubbleClicked(boardPosition);
        }
    }
}
