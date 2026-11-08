using System.Collections;
using TMPro;
using UnityEngine;

namespace Yowol
{
    /// <summary>Gentle float/bob for spirits and pickups.</summary>
    public class Bob : MonoBehaviour
    {
        public float amplitude = 0.15f, frequency = 1.5f;
        Vector3 basePos;
        void Start() { basePos = transform.localPosition; }
        void Update() { transform.localPosition = basePos + Vector3.up * Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) * amplitude; }
    }
}
