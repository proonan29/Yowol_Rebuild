using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class WebPageLink : MonoBehaviour
{
    public string targetURL;
    // ★ 디스코드 아이콘을 클릭했을 때 실행할 함수
    public void OnOpenURL()
    {
        // 문자열 칸이 비어있지 않다면 해당 웹 주소를 브라우저로 엽니다.
        if (!string.IsNullOrEmpty(targetURL))
        {
            Application.OpenURL(targetURL);
        }
        else
        {
            Debug.LogWarning("디스코드 URL 주소가 비어 있습니다!");
        }
    }
}