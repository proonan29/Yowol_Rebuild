using UnityEngine;
using UnityEngine.UI;


public class FrameChange : Button
{
    //현재 술래(선택된 버튼)가 누구인지 저장하는 변수 (관제탑 모니터)
    // public static MenuButton currentTag;

    [Header("공동 스프라이트")]
    [SerializeField]
    public Sprite onSprite; //술래용 스프라이트
    [SerializeField]
    public Sprite offSprite; // 일반 Npc용 스프라이트

    // [Header("랜턴")]
    // public RectTransform lantern;
    private Image currentImage;
    private int index; // 현재로써는 나의 인덱스 값을 모르지만, 메인 메뉴 컨트롤러가 알아서 넣어줄 것.

    // Start is called once before the first execution of Update after the MonoBehaviour is created

    override protected void Awake()
    {
        // 지금 막 시작했으므로, 모든 버튼이 해야될 기본 행위를 한다.
        // 1. OffSprite로 현재 이미지를 교체한다.
        currentImage = GetComponent<Image>();
        if (currentImage != null )
        {
            currentImage.sprite = offSprite; // 기본 시작은 Off로 시작
        }

        // add event for highlighted button

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetIndex(int idx)
    {
        index = idx;
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        // 부모(원래 버튼)의 기본 상태 전환 기능도 정상 실행해줍니다.
        base.DoStateTransition(state, instant);

        // 타겟 이미지가 없다면 실행하지 않습니다.
        if (image == null) return;

        // 핵심 논리: 상태가 'Highlighted(마우스 올림)'라면 술래(On), 그 외에는 모두 Off!
        if (state == SelectionState.Highlighted)
        {
            //SetButtonOnOff(true);
            if (MainMenuController.Instance != null) MainMenuController.Instance.SetSelectedMenu(index);
        }
        else
        {
            // Normal, Pressed, Selected, Disabled 상태일 때
            //SetButtonOnOff(false);
        }

        // 원본 크기로 맞춰주기
        image.SetNativeSize();
    }


    public void SetButtonOnOff(bool onOff)
    {
        if (onOff)
        {
            currentImage.sprite = onSprite;
            //if (MainMenuController.Instance != null) MainMenuController.Instance.SetSelectedMenu(index);
        }
        else
        {
            currentImage.sprite = offSprite;
        }

    }

}
