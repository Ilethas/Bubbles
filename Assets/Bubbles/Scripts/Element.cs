using UnityEngine;

namespace Bubbles.Scripts
{
    public class Element : MonoBehaviour, IInteractableElement
    {
        public Board parentBoard;
        public Vector2Int boardPosition;

        public virtual void OnClick() {}
    }
}