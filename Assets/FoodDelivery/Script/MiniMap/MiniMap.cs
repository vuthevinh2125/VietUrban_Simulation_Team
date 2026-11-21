using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class MiniMap : MonoBehaviour
{
    [SerializeField] private Transform player;
    
    private void Update()
    {
        
 
            Vector3 newPosition = player.position;
            newPosition.y = transform.position.y;
            transform.position = newPosition;
        
        
    }
    
}
