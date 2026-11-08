using System.Collections;
using TMPro;
using UnityEngine;

namespace Yowol
{
    /// <summary>Horizontal parallax relative to the camera.</summary>
    public class Parallax : MonoBehaviour
    {
        [Range(0f, 1f)] public float factor = 0.6f;
        public bool vertical;
        Transform cam;
        Vector3 startPos, camStart;

        void Start()
        {
            cam = Camera.main != null ? Camera.main.transform : null;
            startPos = transform.position;
            if (cam != null) camStart = cam.position;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            Vector3 d = cam.position - camStart;
            transform.position = new Vector3(startPos.x + d.x * factor, vertical ? startPos.y + d.y * factor : startPos.y, startPos.z);
        }
    }
}
