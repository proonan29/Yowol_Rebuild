using System.Collections;
using UnityEngine;

namespace Yowol
{
    /// <summary>
    /// One per stage scene. Spawns the player, sets up camera/UI/music, plays the opening guide lines,
    /// and opens the exit when the stage's enemies are defeated.
    /// </summary>
    public class StageController : MonoBehaviour
    {
        public static StageController Current { get; private set; }

        [Header("Info")]
        public string stageTitle = "STAGE 1";
        public string stageSubtitle = "";

        [Header("Setup")]
        public Transform spawnPoint;
        public Rect cameraBounds = new Rect(-60, -15, 120, 30);
        public float cameraSize = 5f;
        public Vector2 cameraOffset = new Vector2(0f, 1.6f);

        [Header("Audio")]
        public AudioClip bgm;
        public AudioClip ambience;
        [Range(0, 1)] public float ambienceVolume = 0.3f;

        [Header("Story")]
        public string guideName = "잿빛의 불";
        [TextArea] public string[] openingLines;
        [TextArea] public string[] clearLines;

        [Header("Exit")]
        public bool requireClear = true;
        public StageExit exit;

        [Header("Boss")]
        public BossWitch boss;
        [TextArea] public string[] bossIntroLines;
        [TextArea] public string victoryText = "혼령의 마녀를 쓰러뜨렸습니다.\n\n요월 : 혼령의 달  (데모 클리어)";

        public bool Cleared { get; private set; }

        void Awake() { Current = this; }
        void OnDestroy() { if (Current == this) Current = null; }

        IEnumerator Start()
        {
            AudioHub.StopTitleMusic();
            Time.timeScale = 1f;
            GameInput.Locked = false;

            var player = PlayerController.Instance;
            if (player == null && GameAssets.I.playerPrefab != null)
            {
                var go = Instantiate(GameAssets.I.playerPrefab, spawnPoint != null ? spawnPoint.position : Vector3.zero, Quaternion.identity);
                go.name = "Player";
                player = go.GetComponent<PlayerController>();
            }
            else if (player != null && spawnPoint != null) player.transform.position = spawnPoint.position;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.orthographicSize = cameraSize;
                var rig = cam.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
                rig.target = player != null ? player.transform : null;
                rig.bounds = cameraBounds;
                rig.offset = cameraOffset;
                rig.SnapToTarget();
            }

            var ui = GameUI.Create();
            if (bgm != null) AudioHub.I.PlayBgm(bgm, 1.5f);
            if (ambience != null) AudioHub.I.PlayAmbience(ambience, ambienceVolume); else AudioHub.I.StopAmbience();

            if (exit != null) exit.SetOpen(!requireClear);
            if (boss != null) boss.Defeated += OnBossDefeated;

            ui.ShowBanner(stageTitle, stageSubtitle);
            yield return new WaitForSeconds(1.2f);

            if (openingLines != null && openingLines.Length > 0)
                yield return ui.Dialogue(guideName, openingLines);

            if (boss != null)
            {
                if (bossIntroLines != null && bossIntroLines.Length > 0)
                    yield return ui.Dialogue(guideName, bossIntroLines);
                ui.ShowWarning("WARNING  -  " + boss.bossName);
                AudioHub.I.PlayBgm(GameAssets.I.bgmBoss != null ? GameAssets.I.bgmBoss : bgm, 1f);
                yield return new WaitForSeconds(1.0f);
                ui.ShowBoss(boss.bossName, boss.HealthComponent);
                boss.Begin();
            }
        }

        void Update()
        {
            if (Cleared || boss != null) return;
            if (requireClear && EnemyBase.AliveCount <= 0 && Time.timeSinceLevelLoad > 1f) StartCoroutine(ClearRoutine());
        }

        IEnumerator ClearRoutine()
        {
            Cleared = true;
            exit?.SetOpen(true);
            var ui = GameUI.Instance;
            if (ui == null) yield break;
            ui.ShowWarning("길이 열렸습니다  >>", 2f);
            if (clearLines != null && clearLines.Length > 0)
            {
                yield return new WaitForSeconds(0.6f);
                yield return ui.Dialogue(guideName, clearLines);
            }
        }

        void OnBossDefeated() => StartCoroutine(VictoryRoutine());

        IEnumerator VictoryRoutine()
        {
            Cleared = true;
            var ui = GameUI.Instance;
            yield return new WaitForSeconds(2.5f);
            ui?.HideBoss();
            AudioHub.I.StopBgm(2f);
            if (clearLines != null && clearLines.Length > 0) yield return ui.Dialogue(guideName, clearLines);
            PlayerPrefs.DeleteKey("yowol.continueScene");
            ui?.ShowVictory(victoryText);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(cameraBounds.center, cameraBounds.size);
        }
    }
}
