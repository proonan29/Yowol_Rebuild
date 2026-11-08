using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>The guide spirit ("잿빛의 불") floating beside the player.</summary>
    public class GuideSpirit : MonoBehaviour
    {
        public Vector2 offset = new Vector2(-1.2f, 2.4f);
        public float followSpeed = 5f;
        public float bobAmplitude = 0.18f;
        public float bobFrequency = 1.8f;

        void LateUpdate()
        {
            var pc = PlayerController.Instance;
            if (pc == null) return;
            Vector3 target = pc.transform.position + new Vector3(offset.x * pc.FacingSign, offset.y + Mathf.Sin(Time.time * bobFrequency * Mathf.PI) * bobAmplitude, 0f);
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (pc.FacingRight ? 1f : -1f);
            transform.localScale = s;
        }
    }
}
