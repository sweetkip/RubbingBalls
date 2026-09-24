using UnityEngine;

public class FloatingIndicator : MonoBehaviour
{
    [SerializeField] private float distance = 0.1f;
    [SerializeField] private float speed = 2f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.localPosition;
    }

    private void Update()
    {
        float y = Mathf.Sin(Time.time * speed) * distance;

        transform.localPosition =
            startPosition + Vector3.up * y;
    }
}