using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class FadeController : MonoBehaviour
{
    public Image fadeImage;          // Fade_Image 연결
    public float fadeDuration = 0.5f; // 페이드 속도 (0.5초 추천)

    // 열린 팝업창들의 이력을 관리하는 스택
    private Stack<GameObject> popupStack = new Stack<GameObject>();

    void Start()
    {
        // 씬 시작 시 Fade In
        StartCoroutine(FadeInRoutine());
    }

    void Update()
    {
        // ESC 키를 누르면 이전 UI/팝업으로 돌아가기
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackToPreviousUI();
        }
    }

    #region Scene Fade
    public void ChangeSceneWithFade(string sceneName)
    {
        StartCoroutine(FadeOutRoutine(sceneName));
    }

    IEnumerator FadeInRoutine()
    {
        if (fadeImage == null) yield break;

        fadeImage.gameObject.SetActive(true);
        float timer = 0f;
        Color color = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        fadeImage.gameObject.SetActive(false);
    }

    IEnumerator FadeOutRoutine(string sceneName)
    {
        if (fadeImage == null) yield break;

        fadeImage.gameObject.SetActive(true);
        float timer = 0f;
        Color color = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 1f, timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        SceneManager.LoadScene(sceneName);
    }
    #endregion

    #region Popup System (돌아가기 지원)

    // 1. 새 팝업창을 페이드 효과와 함께 열기
    public void OpenPopupWithFade(GameObject popup)
    {
        if (popup == null) return;

        // 팝업 이력 스택에 추가
        popupStack.Push(popup);
        StartCoroutine(FadeOutPopupRoutine(popup));
    }

    // 2. 돌아가기 버튼 또는 ESC 입력 시 이전 창으로 돌아가기 (외부 버튼 OnClick에 연결할 함수)
    public void BackToPreviousUI()
    {
        // 열려있는 팝업이 있다면 가장 최근 팝업 하나를 닫음
        if (popupStack.Count > 0)
        {
            GameObject currentPopup = popupStack.Pop();
            StartCoroutine(FadeInPopupRoutine(currentPopup));
        }
    }

    IEnumerator FadeOutPopupRoutine(GameObject popup)
    {
        if (fadeImage == null) yield break;

        fadeImage.gameObject.SetActive(true);
        float timer = 0f;
        Color color = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            color.a = Mathf.Lerp(0f, 0.6f, timer / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        if (popup != null) popup.SetActive(true);
    }

    IEnumerator FadeInPopupRoutine(GameObject popup)
    {
        // 1. 팝업창 비활성화
        if (popup != null) popup.SetActive(false);

        if (fadeImage != null)
        {
            float timer = 0f;
            Color color = fadeImage.color;

            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                color.a = Mathf.Lerp(0.6f, 0f, timer / fadeDuration);
                fadeImage.color = color;
                yield return null;
            }

            // 2. 페이드가 끝나면 Fade_Image를 확실히 끄기
            fadeImage.gameObject.SetActive(false);
        }
    }

    // 버튼 OnClick ()에 직접 팝업 오브젝트를 넣어서 닫는 함수
    public void ClosePopupDirect(GameObject popup)
    {
        if (popup != null)
        {
            StartCoroutine(FadeInPopupRoutine(popup));
        }
    }

    #endregion
}