using UnityEngine;

public class BreathingArm : MonoBehaviour
{
    [Header("호흡 설정")]
    [Tooltip("숨쉬는 속도입니다.")]
    public float breatheSpeed = 1.5f;

    [Tooltip("팔이 위로 들릴 최대 각도입니다. (메쉬가 깨지지 않게 2~4도 추천)")]
    public float maxRotationAngle = 3f;

    [Header("팔 방향 설정")]
    [Tooltip("왼팔이라면 체크해 주세요. 회전 방향이 반대로 바뀝니다.")]
    public bool isLeftArm = false;

    private Quaternion initialRotation;
    private float timeIndex = 0f;

    void Start()
    {
        // 게임 시작 시점의 본(Bone)의 원래 회전 상태를 쿼터니언으로 '통째로' 안전하게 저장합니다.
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        timeIndex += Time.deltaTime * breatheSpeed;

        // 0 ~ 1 사이를 부드럽게 오가는 호흡 사이클
        float breatheCycle = (Mathf.Sin(timeIndex) + 1f) / 2f;
        float currentOffset = maxRotationAngle * breatheCycle;

        if (isLeftArm)
        {
            currentOffset *= -1f;
        }

        // ★ 핵심: localEulerAngles 대신 AngleAxis를 사용하여 Z축 기준으로만 순수하게 회전하는 쿼터니언을 만듭니다.
        // 이 방식은 스케일이나 다른 축을 절대 건드리지 않아서 메쉬가 깨지지 않습니다.
        Quaternion relativeRotation = Quaternion.AngleAxis(currentOffset, Vector3.forward);

        // 기본 원래 회전값에 계산된 회전값을 곱해줍니다.
        transform.localRotation = initialRotation * relativeRotation;
    }
}