using System;
using Unity.VisualScripting;
using UnityEngine;

public class TopDownCameraFollow : MonoBehaviour
{
    [SerializeField] Vector3 offset = new Vector3(0f, 10f, -8f);
    [SerializeField] float followspeed;

    private Transform target;

    public void SetTarget(Transform newtarget)
    {
        target = newtarget;
    }

    private void LateUpdate()
    {
        if (target == null) { return; }
        Vector3 desiredPosition = target.position + offset;
        transform.position =
            Vector3.Lerp(transform.position, desiredPosition, followspeed * Time.deltaTime);
        transform.LookAt(target.position);
    }
}