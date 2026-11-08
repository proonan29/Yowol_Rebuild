using UnityEngine;

public class ScaleUpDown : MonoBehaviour
{
    [Header("Scale Settings")]
    public float scaleDown = 0.7f; // 최소 크기 비율
    public float scaleSpeed = 5.0f; // 축소 / 확대 속도

    private float accumulatedTime = 0f;
    private float diff;

    void Start()
    {
        diff = 1.0f - scaleDown;
    }

    void Update()
    {
        accumulatedTime += Time.deltaTime * scaleSpeed;
        float currentScale = Mathf.Sin(accumulatedTime) * 0.5f* diff + scaleDown;

        // UI 오브젝트의 LocalScale을 변경합니다.
        transform.localScale = new Vector3(currentScale, currentScale, 1f);
    }
}