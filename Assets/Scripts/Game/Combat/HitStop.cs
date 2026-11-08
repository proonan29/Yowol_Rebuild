using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yowol
{
    /// <summary>Brief freeze on impactful hits.</summary>
    public class HitStop : MonoBehaviour
    {
        static HitStop instance;
        float until;
        bool running;

        public static void Do(float seconds)
        {
            if (Time.timeScale == 0f) return;
            if (instance == null)
            {
                var go = new GameObject("[HitStop]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HitStop>();
            }
            instance.until = Mathf.Max(instance.until, Time.unscaledTime + seconds);
            if (!instance.running) instance.StartCoroutine(instance.Run());
        }

        IEnumerator Run()
        {
            running = true;
            float prev = Time.timeScale;
            Time.timeScale = 0.05f;
            while (Time.unscaledTime < until) yield return null;
            if (Time.timeScale > 0f) Time.timeScale = prev > 0.05f ? prev : 1f;
            running = false;
        }

        public static void SlowMotion(float scale, float seconds)
        {
            if (instance == null)
            {
                var go = new GameObject("[HitStop]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HitStop>();
            }
            instance.StartCoroutine(instance.Slow(scale, seconds));
        }

        IEnumerator Slow(float scale, float seconds)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                Time.timeScale = Mathf.Lerp(scale, 1f, t / 0.4f);
                yield return null;
            }
            Time.timeScale = 1f;
        }
    }
}
