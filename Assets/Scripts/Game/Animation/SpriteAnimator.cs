using System;
using UnityEngine;

namespace Yowol
{
    /// <summary>
    /// Plays SpriteAnimLibrary clips on a SpriteRenderer. Code-driven (no Animator controller),
    /// so gameplay decides exactly which frame is visible and when hit frames happen.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        public SpriteAnimLibrary library;
        public string defaultClip;
        public float speed = 1f;
        public bool unscaledTime;

        SpriteRenderer sr;
        SpriteAnimLibrary.Clip clip;
        int frame;
        float timer;
        bool finished;
        bool manual;

        public event Action<string, int> FrameChanged;
        public event Action<string> Finished;

        public string Current => clip?.name;
        public int Frame => frame;
        public bool IsFinished => finished;
        public SpriteRenderer Renderer => sr != null ? sr : (sr = GetComponent<SpriteRenderer>());
        public SpriteAnimLibrary.Clip CurrentClip => clip;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (clip == null && !string.IsNullOrEmpty(defaultClip)) Play(defaultClip);
        }

        public bool Has(string clipName) => library != null && library.Get(clipName) != null;

        public float LengthOf(string clipName)
        {
            var c = library != null ? library.Get(clipName) : null;
            return c != null ? c.Length : 0f;
        }

        /// <summary>Play a clip. Re-playing the current clip continues it unless restart is true.</summary>
        public void Play(string clipName, bool restart = false)
        {
            if (library == null) return;
            if (!restart && !manual && clip != null && clip.name == clipName && !finished) return;
            var c = library.Get(clipName);
            if (c == null || c.frames == null || c.frames.Length == 0)
            {
                Debug.LogWarning($"[SpriteAnimator] '{name}' missing clip '{clipName}'", this);
                return;
            }
            clip = c;
            frame = 0;
            timer = 0f;
            finished = false;
            manual = false;
            Apply();
        }

        /// <summary>Show a specific frame and stop auto-advancing (used for velocity-driven jump poses).</summary>
        public void SetFrame(string clipName, int index)
        {
            var c = library != null ? library.Get(clipName) : null;
            if (c == null) return;
            index = Mathf.Clamp(index, 0, c.frames.Length - 1);
            bool changed = clip != c || frame != index || !manual;
            clip = c;
            frame = index;
            manual = true;
            finished = false;
            if (changed) Apply();
        }

        void Update()
        {
            if (clip == null || finished || manual) return;
            timer += (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) * speed;
            float d = clip.Duration(frame);
            int guard = 0;
            while (timer >= d && guard++ < 64)
            {
                timer -= d;
                if (frame + 1 < clip.frames.Length) { frame++; Apply(); }
                else if (clip.loop) { frame = 0; Apply(); }
                else
                {
                    finished = true;
                    Finished?.Invoke(clip.name);
                    return;
                }
                d = clip.Duration(frame);
            }
        }

        void Apply()
        {
            Renderer.sprite = clip.frames[frame];
            FrameChanged?.Invoke(clip.name, frame);
        }
    }
}
