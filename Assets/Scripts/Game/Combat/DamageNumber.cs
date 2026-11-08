using System.Collections;
using TMPro;
using UnityEngine;

namespace Yowol
{
    /// <summary>Floating damage numbers.</summary>
    public class DamageNumber : MonoBehaviour
    {
        TextMeshPro text;
        Vector3 velocity;
        float t;
        const float Life = 0.75f;

        public static void Spawn(Vector2 pos, int amount, bool heavy)
        {
            var assets = GameAssets.I;
            var go = new GameObject("DamageNumber");
            go.SetActive(false);   // assign the font before TMP's Awake looks for a default
            go.transform.position = pos + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.3f));
            var tmp = go.AddComponent<TextMeshPro>();
            if (assets != null && assets.titleFont != null) tmp.font = assets.titleFont;
            tmp.text = amount.ToString();
            tmp.fontSize = heavy ? 7f : 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = heavy ? new Color(1f, 0.85f, 0.35f) : Color.white;
            tmp.outlineWidth = 0.25f;
            tmp.outlineColor = new Color32(20, 10, 30, 255);
            tmp.sortingOrder = 200;
            var dn = go.AddComponent<DamageNumber>();
            dn.text = tmp;
            dn.velocity = new Vector3(Random.Range(-0.6f, 0.6f), 3.2f, 0);
            go.SetActive(true);
        }

        void Update()
        {
            t += Time.unscaledDeltaTime;
            velocity.y -= 7f * Time.unscaledDeltaTime;
            transform.position += velocity * Time.unscaledDeltaTime;
            var c = text.color; c.a = 1f - Mathf.Clamp01((t - Life * 0.5f) / (Life * 0.5f)); text.color = c;
            if (t >= Life) Destroy(gameObject);
        }
    }
}
