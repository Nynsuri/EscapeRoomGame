using UnityEngine;

public class HologramFloat : MonoBehaviour
{
    public float floatSpeed = 1f;
    public float floatHeight = 0.1f;
    public float rotateSpeed = 20f;

    Vector3 startPos;

    void Start()
    {
        startPos = transform.position;
    }

    void Update()
    {
        transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);
    }
}