using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    public Transform target;
    public float heightZ = -10f; // camera z
    public bool followRotation = false;

    void LateUpdate()
    {
        if (!target) return;

        transform.position = new Vector3(target.position.x, target.position.y, heightZ);

        if (followRotation)
            transform.rotation = target.rotation;
        else
            transform.rotation = Quaternion.identity;
    }
}
