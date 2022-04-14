using UnityEngine;

namespace Bubbles.Scripts
{
    public class BoardElement : MonoBehaviour, IInteractableBoardElement
    {
        public Board parentBoard;
        public Vector2Int boardPosition;

        public virtual void OnClick() {}
    }
}