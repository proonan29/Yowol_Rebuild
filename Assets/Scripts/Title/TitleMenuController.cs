using UnityEngine;

public class TitleMenuController : MonoBehaviour
{
    [Header("Exit Popup UI")]
    public GameObject quitPopup; //방금 만든 'Exit_Popup' 오브젝트를 인스펙터에서 드래그앤 드롭

   // 1. 타이틀의 '종료' 버튼을 눌렀을 때 실행할 함수
   public void ClickQuitButton()
   {
        if (quitPopup != null)
        {
            quitPopup.SetActive(true); //팝업창 켜기
        }
   }

    // 2.팝업창에서 '예' 버튼을 눌렀을 때 (실제 게임 종료)
    public void ClickYes()
    {
         #if UNITY_EDITOR
         //유니티 에디터 환경에서 테스트 할 때 꺼지도록 처리
        
         UnityEditor.EditorApplication.isPlaying = false;
        
        #else

        // 빌드된 실제 게임(PC, 모바일 등)에서 꺼지도록 처리
        Application.Quit();

        #endif

    }

    // 3. 팝업창에서 '아니오' 버튼을 눌렀을 때 (다시 타이틀로)
    public void ClickNo()
    {
        if (quitPopup != null)
        {
            quitPopup.SetActive(false); //팝업창만 다시 끄기 (타이틀은 그대로 있음)
        }
    }
}
