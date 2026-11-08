using UnityEngine;

public class ClothWindWiggle : MonoBehaviour
{

    [Header("바람 세기 (회전 각도)")]
    [SerializeField] private float windForce = 8f;

    [Header("바람 치는 속도")]
    [SerializeField] private float windSpeed = 3f;

    // Bone 마다 흔들리는 타이밍을 다르게 하기 위한 무작위 오프셋
    private float randomOffset;
    private Quaternion startRotation;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        startRotation = transform.localRotation;
        randomOffset = Random.Range(0f, 100f);
    }

    // Update is called once per frame
    void Update()
    {
        // 삼각함수와 퍼린 노이즈를 섞어 규칙적이지 않은 바람 느낌 연출
        float time = (Time.time *  windSpeed) + randomOffset;
        float wave = Mathf.Sin(time) * Mathf.PerlinNoise(time, 0f);

        // 계산된 각도만큼 Bone을 부드럽게 회전시킴
        float targetZRotation = wave * windForce;
        transform.localRotation = startRotation * Quaternion.Euler(0, 0,targetZRotation);
    }
}
