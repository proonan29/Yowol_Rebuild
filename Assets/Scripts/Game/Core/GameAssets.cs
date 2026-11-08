using TMPro;
using UnityEngine;

namespace Yowol
{
    /// <summary>Shared references, loaded from Resources/GameAssets. Filled by the Yowol builder.</summary>
    [CreateAssetMenu(menuName = "Yowol/Game Assets")]
    public class GameAssets : ScriptableObject
    {
        static GameAssets cached;
        public static GameAssets I
        {
            get
            {
                if (cached == null) cached = Resources.Load<GameAssets>("GameAssets");
                return cached;
            }
        }

        [Header("Fonts")]
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset titleFont;

        [Header("Animation libraries")]
        public SpriteAnimLibrary fx;

        [Header("Generic sprites")]
        public Sprite softCircle;
        public Sprite white;

        [Header("Materials")]
        public Material litSprite;
        public Material unlitSprite;

        [Header("UI")]
        public Sprite hpFrame, hpFill, gaugeFill, slot, slotSelected;
        public Sprite[] weaponIcons = new Sprite[3];
        public Sprite guidePortrait;

        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject reaperPrefab;

        [Header("SFX")]
        public AudioClip sfxSwing, sfxHeavySwing, sfxBow, sfxHit, sfxPlayerHurt, sfxJump, sfxFootstep,
                         sfxDeath, sfxEnemyDeath, sfxBossCast, sfxBossBeam, sfxBossHurt, sfxExplosion, sfxUiClick, sfxSkill;
        public AudioClip[] voiceAttack, voiceHurt;

        [Header("Music")]
        public AudioClip bgmStage, bgmBoss, bgmIntro, ambienceRain;
    }
}
