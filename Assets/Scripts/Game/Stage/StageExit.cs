using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>Stage exit portal. Locked until the stage is cleared (if required).</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class StageExit : MonoBehaviour
    {
        [TextArea] public string[] enterLines;
        public string lockedMessage = "남은 적을 모두 처치하세요";
        public SpriteRenderer glow;
        public Light2D lightSource;

        bool open, used;
        float lastLockedMsg = -9f;

        public void SetOpen(bool value)
        {
            open = value;
            if (glow != null) glow.enabled = value;
            if (lightSource != null) lightSource.enabled = value;
        }

        void Update()
        {
            if (glow != null && open)
            {
                float k = 0.6f + Mathf.Sin(Time.time * 3f) * 0.25f;
                glow.color = new Color(0.55f, 0.85f, 1f, k);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (used || other.GetComponentInParent<PlayerController>() == null) return;
            if (!open)
            {
                if (Time.time - lastLockedMsg > 3f && GameUI.Instance != null)
                {
                    GameUI.Instance.ShowWarning(lockedMessage, 1.6f);
                    lastLockedMsg = Time.time;
                }
                return;
            }
            used = true;
            StartCoroutine(Leave());
        }

        IEnumerator Leave()
        {
            PlayerController.Instance?.Freeze(5f);
            if (enterLines != null && enterLines.Length > 0 && GameUI.Instance != null)
                yield return GameUI.Instance.Dialogue(StageController.Current != null ? StageController.Current.guideName : "", enterLines);
            GameFlow.LoadNext();
        }
    }
}
