using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>One-shot guide dialogue when the player walks in.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class GuideTrigger : MonoBehaviour
    {
        [TextArea] public string[] lines;
        bool done;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (done || other.GetComponentInParent<PlayerController>() == null || GameUI.Instance == null) return;
            if (GameUI.Instance.DialogueOpen) return;
            done = true;
            string speaker = StageController.Current != null ? StageController.Current.guideName : "";
            GameUI.Instance.StartCoroutine(GameUI.Instance.Dialogue(speaker, lines));
        }
    }
}
