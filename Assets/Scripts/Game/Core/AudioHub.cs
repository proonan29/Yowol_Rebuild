using System.Collections;
using UnityEngine;

namespace Yowol
{
    /// <summary>Persistent BGM (crossfade), ambience and pooled SFX.</summary>
    public class AudioHub : MonoBehaviour
    {
        static AudioHub instance;
        AudioSource bgmA, bgmB, ambience;
        AudioSource[] sfx;
        int sfxIndex;
        Coroutine bgmFade;

        public static float BgmVolume = 0.45f;
        public static float SfxVolume = 0.8f;

        public static AudioHub I
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[AudioHub]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<AudioHub>();
                    instance.Init();
                }
                return instance;
            }
        }

        void Init()
        {
            bgmA = MakeSource(true);
            bgmB = MakeSource(true);
            ambience = MakeSource(true);
            sfx = new AudioSource[16];
            for (int i = 0; i < sfx.Length; i++) sfx[i] = MakeSource(false);
        }

        AudioSource MakeSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        /// <summary>The title scene keeps its own DontDestroyBGM object; remove it once gameplay starts.</summary>
        public static void StopTitleMusic()
        {
            foreach (var b in FindObjectsByType<DontDestroyBGM>())
                Destroy(b.gameObject);
        }

        public void PlayBgm(AudioClip clip, float fade = 1.2f, float volume = 1f)
        {
            if (clip == null) return;
            if (bgmA.clip == clip && bgmA.isPlaying) return;
            if (bgmFade != null) StopCoroutine(bgmFade);
            (bgmA, bgmB) = (bgmB, bgmA);          // bgmA becomes the new track
            bgmA.clip = clip;
            bgmA.volume = 0f;
            bgmA.Play();
            bgmFade = StartCoroutine(CrossFade(fade, volume));
        }

        public void StopBgm(float fade = 1f)
        {
            if (bgmFade != null) StopCoroutine(bgmFade);
            bgmFade = StartCoroutine(FadeOutBoth(fade));
        }

        IEnumerator CrossFade(float duration, float volume)
        {
            float startB = bgmB.volume;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                bgmA.volume = Mathf.Lerp(0f, BgmVolume * volume, k);
                bgmB.volume = Mathf.Lerp(startB, 0f, k);
                yield return null;
            }
            bgmA.volume = BgmVolume * volume;
            bgmB.Stop();
        }

        IEnumerator FadeOutBoth(float duration)
        {
            float a = bgmA.volume, b = bgmB.volume;
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / duration;
                bgmA.volume = a * k; bgmB.volume = b * k;
                yield return null;
            }
            bgmA.Stop(); bgmB.Stop();
            bgmA.clip = null;
        }

        public void PlayAmbience(AudioClip clip, float volume = 0.35f)
        {
            if (clip == null) { ambience.Stop(); return; }
            if (ambience.clip == clip && ambience.isPlaying) return;
            ambience.clip = clip;
            ambience.volume = volume * SfxVolume;
            ambience.Play();
        }

        public void StopAmbience() => ambience.Stop();

        public static void Sfx(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null) return;
            var hub = I;
            var s = hub.sfx[hub.sfxIndex];
            hub.sfxIndex = (hub.sfxIndex + 1) % hub.sfx.Length;
            s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            s.PlayOneShot(clip, volume * SfxVolume);
        }
    }
}
