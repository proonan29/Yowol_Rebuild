using UnityEngine;

public class ExitPopupManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject exitPopupPanel; // Esc_Popup_UI
    [SerializeField] private GameObject mainTitleGroup; // MainTitle_Group (필요시)

    // 1. 종료 버튼을 눌렀을 때 팝업 열기
    public void OpenExitPopup()
    {
        if (exitPopupPanel != null)
        {
            exitPopupPanel.SetActive(true);
        }

        PlayClickSound();
    }

    // 2. '예' 버튼을 눌렀을 때 : 게임 종료
    public void OnClickYesExit()
    {
        PlayClickSound();

#if UNITY_EDITOR
        // 에디터 실행 중일 때
        UnityEditor.EditorApplication.isPlaying = false;
#else
            // 빌드된 실제 게임 실행 중일 때
            Application.Quit();
#endif
    }

    // 3. '아니오' 버튼을 눌렀을 때 : 팝업 닫고 전 화면으로
    public void OnClickNoCancel()
    {
        if (exitPopupPanel != null)
        {
            exitPopupPanel.SetActive(false);
        }

        PlayClickSound();
    }

    private void PlayClickSound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClickSound();
        }
    }
}