using System.Collections;
using TMPro;
using UnityEngine;

namespace Yowol
{
    /// <summary>Smooth side-scroll camera with look-ahead, bounds and shake.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        public Transform target;
        public Vector2 offset = new Vector2(0f, 1.6f);
        public float smoothTime = 0.18f;
        public float lookAhead = 1.6f;
        public bool useBounds = true;
        public Rect bounds = new Rect(-50, -20, 100, 40);

        Camera cam;
        Vector3 velocity;
        float lookX;
        float shakeAmp, shakeUntil, shakeDur;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
        }

        public static void Shake(float amplitude, float duration = 0.2f)
        {
            if (Instance == null) return;
            if (amplitude >= Instance.shakeAmp * Mathf.Clamp01((Instance.shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, Instance.shakeDur)))
            {
                Instance.shakeAmp = amplitude;
                Instance.shakeDur = duration;
                Instance.shakeUntil = Time.unscaledTime + duration;
            }
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            transform.position = Clamp(Desired());
            velocity = Vector3.zero;
        }

        Vector3 Desired()
        {
            float facing = target.localScale.x >= 0 ? 1f : -1f;
            var pc = target.GetComponent<PlayerController>();
            if (pc != null) facing = pc.FacingRight ? 1f : -1f;
            lookX = Mathf.Lerp(lookX, facing * lookAhead, Time.deltaTime * 2f);
            return new Vector3(target.position.x + offset.x + lookX, target.position.y + offset.y, transform.position.z);
        }

        Vector3 Clamp(Vector3 p)
        {
            if (!useBounds) return p;
            float h = cam.orthographicSize, w = h * cam.aspect;
            float minX = bounds.xMin + w, maxX = bounds.xMax - w;
            float minY = bounds.yMin + h, maxY = bounds.yMax - h;
            p.x = minX > maxX ? bounds.center.x : Mathf.Clamp(p.x, minX, maxX);
            p.y = minY > maxY ? bounds.center.y : Mathf.Clamp(p.y, minY, maxY);
            return p;
        }

        void LateUpdate()
        {
            if (target != null)
            {
                var goal = Clamp(Desired());
                var p = Vector3.SmoothDamp(transform.position - ShakeOffsetLast, goal, ref velocity, smoothTime);
                ShakeOffsetLast = Vector3.zero;
                transform.position = p;
            }
            if (Time.unscaledTime < shakeUntil)
            {
                float k = (shakeUntil - Time.unscaledTime) / Mathf.Max(0.01f, shakeDur);
                var o = (Vector3)(Random.insideUnitCircle * shakeAmp * k);
                transform.position += o;
                ShakeOffsetLast = o;
            }
        }

        Vector3 ShakeOffsetLast;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }
}
