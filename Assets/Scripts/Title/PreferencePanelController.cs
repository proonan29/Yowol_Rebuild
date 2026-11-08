using UnityEngine;

public class PreferencePanelController : MonoBehaviour
{
    [SerializeField] private GameObject fadeImage; // Fade_Image 연결용

    public void ClosePreferenceUI()
    {
        // 1. 페이드 가림막 이미지도 함께 즉시 끄기
        if (fadeImage != null)
        {
            fadeImage.SetActive(false);
        }

        // 2. 자기 자신(Preference_UI) 비활성화
        gameObject.SetActive(false);

        // 3. 사운드 재생
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClickSound();
        }
    }
}