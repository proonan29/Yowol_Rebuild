using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Yowol
{
    /// <summary>Persistent full-screen black fader used for every scene change.</summary>
    public class ScreenFader : MonoBehaviour
    {
        static ScreenFader instance;
        CanvasGroup group;
        bool busy;

        public static ScreenFader Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[ScreenFader]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<ScreenFader>();
                    instance.Build();
                }
                return instance;
            }
        }

        public bool Busy => busy;

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            img.GetComponent<Image>().color = Color.black;
        }

        public void FadeToScene(string scene, float duration = 0.6f)
        {
            if (busy) return;
            StartCoroutine(LoadRoutine(scene, duration));
        }

        IEnumerator LoadRoutine(string scene, float duration)
        {
            busy = true;
            group.blocksRaycasts = true;
            yield return Fade(group.alpha, 1f, duration);
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            Time.timeScale = 1f;
            yield return Fade(1f, 0f, duration);
            group.blocksRaycasts = false;
            busy = false;
        }

        public IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            group.alpha = to;
        }

        /// <summary>Fade to black and back without a scene change (e.g. respawn).</summary>
        public void Blink(System.Action atBlack, float duration = 0.35f)
        {
            StartCoroutine(BlinkRoutine(atBlack, duration));
        }

        IEnumerator BlinkRoutine(System.Action atBlack, float duration)
        {
            yield return Fade(group.alpha, 1f, duration);
            atBlack?.Invoke();
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Fade(1f, 0f, duration);
        }
    }
}
