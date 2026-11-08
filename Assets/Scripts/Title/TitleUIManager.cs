using UnityEngine;

public class TitleUIManager : MonoBehaviour
{
    [SerializeField] private GameObject titleUI;      // MainTitle_Group
    [SerializeField] private GameObject preferenceUI; // Preference_UI

    // '옵션' 버튼 눌렀을 때 호출
    public void OpenOption()
    {
        if (titleUI != null) titleUI.SetActive(false);       // 타이틀 끄기
        if (preferenceUI != null) preferenceUI.SetActive(true); // 옵션창 켜기
    }

    // '돌아가기' 버튼 눌렀을 때 호출
    public void CloseOption()
    {
        if (preferenceUI != null) preferenceUI.SetActive(false); // 옵션창 끄기
        if (titleUI != null) titleUI.SetActive(true);          // 타이틀 다시 켜기! (이 부분이 누락되어 있던 것)

        // 클릭 소리 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClickSound();
        }
    }
}