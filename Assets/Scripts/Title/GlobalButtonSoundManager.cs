using UnityEngine;
using UnityEngine.UI;

public class GlobalButtonSound : MonoBehaviour
{
    private void Start()
    {
        RegisterAllButtons();
    }

    // 씬 내의 모든 Button 컴포넌트를 찾아 클릭 소리 리스너 추가
    public void RegisterAllButtons()
    {
        Button[] allButtons = Resources.FindObjectsOfTypeAll<Button>();

        foreach (Button btn in allButtons)
        {
            // 씬에 실제로 존재하고 활성화된 버튼에만 등록
            if (btn.gameObject.scene.isLoaded)
            {
                btn.onClick.RemoveListener(PlaySound); // 중복 등록 방지
                btn.onClick.AddListener(PlaySound);
            }
        }
    }

    private void PlaySound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClickSound();
        }
    }
}