using UnityEngine;
public class AudioZone : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private float maxVolume = 0.8f;
    [SerializeField] private Transform player;
    [SerializeField] private Collider zoneCollider;
    [SerializeField] private float fadeDistance = 20;
    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }
    

    void Update()
    {
        Vector3 closest = zoneCollider.ClosestPoint(player.position);
        float distance = Vector3.Distance(player.position, closest);
        float distanceToVolume = Mathf.Clamp01(1 - distance / fadeDistance);
        float result = distanceToVolume * maxVolume;
        audioSource.volume = result;

    }
}
