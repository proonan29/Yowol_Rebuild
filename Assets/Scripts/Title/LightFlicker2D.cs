using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightFlicker2D : MonoBehaviour
{
    private Light2D light2d;
    private float baseIntensity;

    [Header("Flicker Settings")]
    [SerializeField] private float intensityDelta = 0.5f; // 원래 강도에서 더해지거나 빼질 강도
    [SerializeField] private float speed = 5.0f; // 깜빡임 속도

    private void Start()
    {
        light2d = GetComponent<Light2D>();
        if (light2d == null)
        {
            Debug.LogError("Light2D 컴포넌트를 찾을 수 없습니다!");
            enabled = false;
            return;
        }

        baseIntensity = light2d.intensity;
    }

    private void Update()
    {
        // 퍼린 노이즈를 사용하여 자연스러운 깜빡임 효과 연산
        float noiseValue = Mathf.PerlinNoise(Time.time * speed, 0.0f);
        light2d.intensity = baseIntensity + (noiseValue - 0.5f) * intensityDelta;
    }
}