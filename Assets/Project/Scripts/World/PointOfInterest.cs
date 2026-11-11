using UnityEngine;

public class PointOfInterest : MonoBehaviour
{
    public float minWaitTime = 5f;
    public float maxWaitTime = 15f;
    public string animTriggerName = "Idle";

    public bool isSchoolGate = false;
    public bool isBusStop = false;

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawCube(transform.position + Vector3.up, new Vector3(0.5f, 2f, 0.5f));

        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward);
    }
}