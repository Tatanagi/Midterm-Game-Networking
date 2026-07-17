using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform camTransform;

    private void Start()
    {
        camTransform = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (camTransform != null)
        {
            transform.LookAt(transform.position + camTransform.rotation * Vector3.forward,
                             camTransform.rotation * Vector3.up);
        }
    }
}