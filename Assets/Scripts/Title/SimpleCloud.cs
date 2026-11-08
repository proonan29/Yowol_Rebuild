using UnityEngine;
using UnityEngine.UI;

public class SimpleCloud : MonoBehaviour
{
    [Header("이동 속도")]
    public float moveSpeed = 200f;     // 오른쪽 이동 속도

    [Header("크기 조절")]
    public float shrinkSpeed = 0.5f;   // 작아지는 속도

    [Header("투명도(알파) 조절")]
    public float fadeInSpeed = 3.0f;   // 나타나는 속도 (페이드 인)
    public float fadeOutSpeed = 1.0f;  // 사라지는 속도 (페이드 아웃)

    private RectTransform rectTransform;
    private Graphic cloudGraphic;

    private Vector2 startPosition;
    private Vector3 startScale;
    private Color baseColor;           // 원래 RGB 색상 저장용

    private enum CloudState { FadeIn, FadeOut }
    private CloudState currentState;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        cloudGraphic = GetComponent<Graphic>();

        startPosition = rectTransform.anchoredPosition;
        startScale = transform.localScale;

        if (cloudGraphic != null)
        {
            baseColor = cloudGraphic.color;
        }

        // 시작할 때 완전히 투명한 상태로 초기화
        ResetCloud();
    }

    void Update()
    {
        // 1. 오른쪽(+X)으로 항상 이동
        rectTransform.anchoredPosition += Vector2.right * moveSpeed * Time.deltaTime;

        // 2. 크기는 항상 서서히 감소
        transform.localScale -= Vector3.one * shrinkSpeed * Time.deltaTime;

        // 3. 투명도(알파) 처리
        if (cloudGraphic != null)
        {
            Color color = cloudGraphic.color;

            if (currentState == CloudState.FadeIn)
            {
                // 완전히 투명(0)에서 시작해 알파값을 올림 (서서히 나타남)
                color.a += fadeInSpeed * Time.deltaTime;

                // 알파값이 1(완전 불투명) 이상이 되면 사라지는 단계(FadeOut)로 전환
                if (color.a >= 1.0f)
                {
                    color.a = 1.0f;
                    currentState = CloudState.FadeOut;
                }
            }
            else if (currentState == CloudState.FadeOut)
            {
                // 알파값을 낮춤 (서서히 사라짐)
                color.a -= fadeOutSpeed * Time.deltaTime;
            }

            cloudGraphic.color = color;
        }

        // 4. 크기가 0 이하가 되거나, 페이드 아웃 도중 완전히 투명해지면 리셋
        bool isFullyFadedOut = (currentState == CloudState.FadeOut) && (cloudGraphic != null && cloudGraphic.color.a <= 0f);

        if (transform.localScale.x <= 0f || isFullyFadedOut)
        {
            ResetCloud();
        }
    }

    // 초기 상태(처음 위치, 원래 크기, 완전 투명)로 리셋
    void ResetCloud()
    {
        rectTransform.anchoredPosition = startPosition;
        transform.localScale = startScale;
        currentState = CloudState.FadeIn;

        if (cloudGraphic != null)
        {
            // 알파값을 0(완전 투명)으로 세팅해서 시작
            Color initColor = baseColor;
            initColor.a = 0f;
            cloudGraphic.color = initColor;
        }
    }
}