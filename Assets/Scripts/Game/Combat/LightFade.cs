using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    public class LightFade : MonoBehaviour
    {
        public float duration = 0.25f;
        Light2D light2D;
        float start, t;

        void Awake() { light2D = GetComponent<Light2D>(); start = light2D.intensity; }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            light2D.intensity = Mathf.Lerp(start, 0f, t / duration);
            if (t >= duration) Destroy(gameObject);
        }
    }
}
