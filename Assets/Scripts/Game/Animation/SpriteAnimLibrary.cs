using System.Collections.Generic;
using UnityEngine;

namespace Yowol
{
    /// <summary>Named sprite clips with per-frame durations. Generated from the normalized art JSON.</summary>
    [CreateAssetMenu(menuName = "Yowol/Sprite Anim Library")]
    public class SpriteAnimLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Clip
        {
            public string name;
            public bool loop;
            public Sprite[] frames;
            public float[] durations;

            public float Duration(int i) =>
                durations != null && durations.Length > 0 ? durations[Mathf.Clamp(i, 0, durations.Length - 1)] : 0.1f;

            public float Length
            {
                get
                {
                    float s = 0f;
                    for (int i = 0; i < frames.Length; i++) s += Duration(i);
                    return s;
                }
            }

            /// <summary>Time from clip start until frame i begins.</summary>
            public float TimeUntilFrame(int i)
            {
                float s = 0f;
                for (int k = 0; k < Mathf.Min(i, frames.Length); k++) s += Duration(k);
                return s;
            }
        }

        public List<Clip> clips = new List<Clip>();
        Dictionary<string, Clip> map;

        public Clip Get(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            if (map == null || map.Count != clips.Count)
            {
                map = new Dictionary<string, Clip>();
                foreach (var c in clips) map[c.name] = c;
            }
            return map.TryGetValue(clipName, out var clip) ? clip : null;
        }

        void OnValidate() { map = null; }
    }
}
