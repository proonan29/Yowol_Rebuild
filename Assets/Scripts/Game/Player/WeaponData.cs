using UnityEngine;

namespace Yowol
{
    public enum WeaponId { Sword = 0, Bow = 1, GreatSword = 2 }

    /// <summary>Tuning for one weapon. Skill behaviour lives in PlayerCombat; numbers live here.</summary>
    [CreateAssetMenu(menuName = "Yowol/Weapon")]
    public class WeaponData : ScriptableObject
    {
        public WeaponId id;
        public string displayName;
        public Sprite icon;

        [Header("Animation clips")]
        public string idleClip;
        public string attackClip;
        public string jumpClip;          // running jump
        public string jumpInPlaceClip;   // standing jump

        [Header("Normal attack")]
        public int damage = 15;
        public float cooldown = 0.3f;
        [Tooltip("Frame of the attack clip on which the hit (or arrow release) happens.")]
        public int hitFrame = 1;
        public Vector2 hitOffset = new Vector2(1.1f, 1.0f);
        public Vector2 hitSize = new Vector2(2.2f, 1.8f);
        public float knockback = 3f;
        public float lungeSpeed = 2f;
        [Range(0f, 1f)] public float moveWhileAttacking = 0.2f;
        public string[] comboFx;          // FX clip per combo step (cycled)
        public float fxScale = 1f;
        public Vector2 fxOffset = new Vector2(1.0f, 1.0f);

        [Header("Skills (E / Q)")]
        public string skill1Name;
        public float skill1Cooldown = 10f;
        public Sprite skill1Icon;
        public string skill2Name;
        public float skill2Cooldown = 30f;
        public Sprite skill2Icon;

        [Header("Skill numbers")]
        public int skill1Hits = 10;
        public int skill1Damage = 10;
        public int skill2Hits = 6;
        public int skill2Damage = 38;
    }
}
