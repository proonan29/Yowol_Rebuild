using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenuController : MonoBehaviour
{
    // 전역에서 누구나 접근할 수 있도록 static으로 선언 (유일한 관제탑 주소)
    public static MainMenuController Instance { get; private set; } = null;

    [System.Serializable]
    public struct MenuPair
    {
        public Image targetButtonImage; // 항상 켜져 있을 버튼 이미지 컴포넌트 (예: Play_Button_Off 등록)
        public Sprite onSprite;         // 흰색 불 켜진 png 이미지 (프로젝트 창에서 드래그)
        public Sprite offSprite;        // 회색 불 꺼진 png 이미지 (프로젝트 창에서 드래그)
    }

    [Header("Menu Buttons")]
    public FrameChange[] menuButtons;   // 플레이, 불러오기, 설정, 종료 순서대로 등록

    [Header("Lantern Settings")]
    public GameObject lantern;    // 등불 오브젝트 (Select)의 RectTransform
    public float lanternXOffset = -120f; // 등불을 글자 왼쪽으로 얼마나 띄울지 조절하는 값

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 모든 버튼에게 일률적으로 시리얼 넘버를 부여.
        for (int i = 0; i < menuButtons.Length; i++)
        {
            var button = menuButtons[i];
            if (button != null) 
            {
                button.SetIndex(i);
            }
        }

        // 게임 시작 시: 0번(플레이)만 On 이미지로 세팅하고 나머지는 Off 이미지로 세팅
        SetSelectedMenu(0);

        /*
        // 마우스 감지 이벤트를 자동으로 등록합니다.
        for (int i = 0; i < menuButtons.Length; i++)
        {
            int index = i;

            // 이미지 오브젝트에 마우스 감지 컴포넌트 추가
            EventTrigger trigger = menuButtons[index].targetButtonImage.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = menuButtons[index].targetButtonImage.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { SetSelectedMenu(index); });
            trigger.triggers.Add(entry);
        }
        */
    }

    public void SetSelectedMenu(int selectedIndex)
    {
        // 먼저 모든 버튼에서 선택지를 없앤다.
        for (int i = 0; i < menuButtons.Length; i++)
        {
            //if (menuButtons[i].targetButtonImage == null) continue;

            if (i == selectedIndex)
            {
                //선택 되었으므로 On 스프라이트를 이용한다.
                menuButtons[i].SetButtonOnOff(true);
                // 등불도 이곳으로 이동되어야 함.
                lantern.transform.localPosition = menuButtons[i].transform.localPosition + new Vector3(lanternXOffset, 0,0);
            }
            else
            {
                menuButtons[i].SetButtonOnOff(false);
            }
        }
    }
}