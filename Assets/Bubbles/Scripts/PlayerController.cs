using UnityEngine;

namespace Bubbles.Scripts
{
    public class PlayerController : MonoBehaviour
    {
        public Camera playerCamera;
        
        // Start is called before the first frame update
        void Start()
        {
        
        }

        // Update is called once per frame
        void Update()
        {
            if (Input.GetMouseButtonUp(0)) 
            {
                Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 10000f))
                {
                    IInteractableElement element = hit.transform.gameObject.GetComponent<IInteractableElement>();
                    element?.OnClick();
                }
            }
        }
    }
}
