using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Yowol
{
    /// <summary>
    /// Prologue cutscene (separated from the old Stage 1): illustrated panels with typed lines.
    /// Space/F/Enter/Click: next.  S/Esc: skip.  Ends by loading Stage1.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [System.Serializable]
        public class Panel
        {
            public Sprite image;
            [TextArea(1, 3)] public string text;
            public AudioClip bgm;
        }

        public Panel[] panels;
        public float typeSpeed = 0.05f;

        Image image, imageNext;
        TextMeshProUGUI text, prompt;
        CanvasGroup textGroup;
        bool skipping;

        void Start()
        {
            AudioHub.StopTitleMusic();
            GameInput.Locked = false;
            Build();
            StartCoroutine(Run());
        }

        void Build()
        {
            var canvas = UIBuild.Canvas("IntroCanvas", 10, transform);
            var root = canvas.transform;
            var bg = UIBuild.Stretch("Black", root).gameObject.AddComponent<Image>();
            bg.color = Color.black;
            image = MakeImage(root, "Image");
            imageNext = MakeImage(root, "ImageNext");
            imageNext.color = new Color(1, 1, 1, 0);

            UIBuild.Image("BarTop", root, GameAssets.I.white, Color.black, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(2400, 90));
            var barBottom = UIBuild.Image("BarBottom", root, GameAssets.I.white, new Color(0, 0, 0, 0.88f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(2400, 230));
            textGroup = barBottom.gameObject.AddComponent<CanvasGroup>();
            text = UIBuild.Text("Line", barBottom.transform, "", 46, Color.white, TextAlignmentOptions.Center,
                                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(1600, 140), true);
            prompt = UIBuild.Text("Prompt", barBottom.transform, "[Space] 다음      [S] 건너뛰기", 24, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.BottomRight,
                                  new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(420, 18), new Vector2(1400, 34));
        }

        Image MakeImage(Transform root, string n)
        {
            var rt = UIBuild.Stretch(n, root);
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            var fitter = rt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
            return img;
        }

        IEnumerator Run()
        {
            yield return new WaitForSeconds(0.3f);
            Sprite current = null;
            for (int i = 0; i < panels.Length && !skipping; i++)
            {
                var p = panels[i];
                if (p.bgm != null) AudioHub.I.PlayBgm(p.bgm, 1.5f, 1f);
                if (p.image != null && p.image != current)
                {
                    yield return Crossfade(p.image);
                    current = p.image;
                }
                yield return TypeLine(p.text);
                if (skipping) break;
                prompt.enabled = true;
                while (!GameInput.AdvancePressed && !skipping)
                {
                    CheckSkip();
                    yield return null;
                }
                yield return null;
            }
            AudioHub.I.StopBgm(1.2f);
            GameFlow.Load(GameFlow.Stage1);
        }

        void Update() => CheckSkip();

        void CheckSkip()
        {
            if (!skipping && GameInput.SkipPressed) skipping = true;
        }

        IEnumerator Crossfade(Sprite next)
        {
            imageNext.sprite = next;
            FitAspect(imageNext);
            float d = 0.7f;
            for (float t = 0; t < d && !skipping; t += Time.deltaTime)
            {
                imageNext.color = new Color(1, 1, 1, t / d);
                image.color = new Color(1, 1, 1, 1f - t / d);
                yield return null;
            }
            image.sprite = next;
            FitAspect(image);
            image.color = Color.white;
            imageNext.color = new Color(1, 1, 1, 0);
        }

        static void FitAspect(Image img)
        {
            var f = img.GetComponent<AspectRatioFitter>();
            if (img.sprite != null && f != null) f.aspectRatio = img.sprite.rect.width / img.sprite.rect.height;
        }

        IEnumerator TypeLine(string line)
        {
            prompt.enabled = false;
            text.text = line;
            text.maxVisibleCharacters = 0;
            yield return null;
            float t = 0f;
            while (text.maxVisibleCharacters < line.Length && !skipping)
            {
                if (GameInput.AdvancePressed) { text.maxVisibleCharacters = line.Length; yield return null; yield break; }
                t += Time.deltaTime;
                text.maxVisibleCharacters = Mathf.Min(line.Length, Mathf.FloorToInt(t / typeSpeed));
                yield return null;
            }
        }
    }
}
