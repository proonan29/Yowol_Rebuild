using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Yowol
{
    /// <summary>
    /// In-game UI built from code: HUD (HP, weapons, skills), boss bar, stage banner, warnings,
    /// guide dialogue, pause, game over and victory panels.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        public static GameUI Instance { get; private set; }

        Canvas canvas;
        RectTransform hud;
        Image hpFill; TextMeshProUGUI hpText;
        readonly Image[] weaponSlots = new Image[3];
        readonly Image[] weaponIcons = new Image[3];
        readonly Image[] skillCooldown = new Image[2];
        readonly TextMeshProUGUI[] skillNames = new TextMeshProUGUI[2];
        readonly TextMeshProUGUI[] skillTimers = new TextMeshProUGUI[2];
        RectTransform bossBar; Image bossFill; TextMeshProUGUI bossName; Health bossHealth;
        CanvasGroup banner; TextMeshProUGUI bannerTitle, bannerSub;
        CanvasGroup warning; TextMeshProUGUI warningText;
        CanvasGroup dialogue; TextMeshProUGUI dialogueName, dialogueBody, dialoguePrompt; Image dialoguePortrait;
        GameObject pausePanel, gameOverPanel, victoryPanel;
        TextMeshProUGUI victoryText;
        float shownHp = 1f;

        public bool DialogueOpen { get; private set; }
        public bool Paused { get; private set; }

        public static GameUI Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("[GameUI]");
            Instance = go.AddComponent<GameUI>();
            Instance.Build();
            return Instance;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Build()
        {
            EnsureEventSystem();
            canvas = UIBuild.Canvas("Canvas", 100, transform);
            var root = canvas.transform;
            var a = GameAssets.I;

            // ---------------- HUD
            hud = UIBuild.Stretch("HUD", root);
            var hpBack = UIBuild.Image("HP_Frame", hud, a.hpFrame, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -36), new Vector2(560, 48));
            if (a.hpFrame == null) hpBack.color = new Color(0, 0, 0, 0.6f);
            hpFill = UIBuild.Image("HP_Fill", hpBack.transform, a.hpFill, new Color(1f, 0.45f, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(508, 34));
            hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal; hpFill.fillOrigin = 0;
            if (a.hpFill == null) hpFill.sprite = a.white;
            hpText = UIBuild.Text("HP_Text", hpBack.transform, "100 / 100", 24, Color.white, TextAlignmentOptions.Center,
                                  new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 1), new Vector2(500, 40));
            hpText.outlineWidth = 0.2f;

            string[] keys = { "1", "2", "3" };
            for (int i = 0; i < 3; i++)
            {
                var slot = UIBuild.Image("Weapon" + i, hud, a.slot, Color.white, new Vector2(0, 1), new Vector2(0, 1), new Vector2(44 + i * 92, -100), new Vector2(82, 82));
                if (a.slot == null) slot.color = new Color(0, 0, 0, 0.5f);
                weaponSlots[i] = slot;
                weaponIcons[i] = UIBuild.Image("Icon", slot.transform, a.weaponIcons.Length > i ? a.weaponIcons[i] : null, Color.white,
                                               new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58, 58), true);
                var k = UIBuild.Text("Key", slot.transform, keys[i], 20, new Color(1, 1, 1, 0.85f), TextAlignmentOptions.TopLeft,
                                     new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -2), new Vector2(30, 30));
                k.outlineWidth = 0.25f;
            }

            string[] skillKeys = { "E", "Q" };
            for (int i = 0; i < 2; i++)
            {
                var box = UIBuild.Image("Skill" + i, hud, a.slot, Color.white, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150 + i * 110, 40), new Vector2(96, 96));
                if (a.slot == null) box.color = new Color(0, 0, 0, 0.5f);
                var key = UIBuild.Text("Key", box.transform, skillKeys[i], 40, Color.white, TextAlignmentOptions.Center,
                                       new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(90, 60), true);
                key.outlineWidth = 0.2f;
                skillCooldown[i] = UIBuild.Image("Cooldown", box.transform, a.white, new Color(0, 0, 0, 0.65f),
                                                 new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
                skillCooldown[i].type = Image.Type.Filled; skillCooldown[i].fillMethod = Image.FillMethod.Radial360;
                skillCooldown[i].fillOrigin = (int)Image.Origin360.Top; skillCooldown[i].fillClockwise = false;
                skillTimers[i] = UIBuild.Text("Timer", box.transform, "", 28, new Color(1f, 0.9f, 0.6f), TextAlignmentOptions.Center,
                                              new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 60));
                skillNames[i] = UIBuild.Text("Name", box.transform, "", 18, new Color(1, 1, 1, 0.9f), TextAlignmentOptions.Center,
                                             new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(140, 30));
                skillNames[i].outlineWidth = 0.25f;
            }

            // ---------------- Boss bar
            bossBar = UIBuild.Rect("BossBar", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(1000, 70));
            var bb = UIBuild.Image("Back", bossBar, a.white, new Color(0.05f, 0.03f, 0.08f, 0.8f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(1000, 26));
            bossFill = UIBuild.Image("Fill", bb.transform, a.gaugeFill != null ? a.gaugeFill : a.white, new Color(0.7f, 0.35f, 1f),
                                     new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(992, 18));
            bossFill.type = Image.Type.Filled; bossFill.fillMethod = Image.FillMethod.Horizontal;
            bossName = UIBuild.Text("Name", bossBar, "", 30, new Color(0.9f, 0.85f, 1f), TextAlignmentOptions.Bottom,
                                    new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 6), new Vector2(1000, 44), true);
            bossName.outlineWidth = 0.2f;
            bossBar.gameObject.SetActive(false);

            // ---------------- Banner & warning
            var bannerRt = UIBuild.Rect("Banner", root, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 220));
            banner = bannerRt.gameObject.AddComponent<CanvasGroup>(); banner.alpha = 0f; banner.blocksRaycasts = false;
            bannerTitle = UIBuild.Text("Title", bannerRt, "", 96, Color.white, TextAlignmentOptions.Center,
                                       new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 130), true);
            bannerTitle.outlineWidth = 0.15f; bannerTitle.outlineColor = new Color32(10, 10, 30, 255);
            bannerSub = UIBuild.Text("Sub", bannerRt, "", 40, new Color(0.8f, 0.9f, 1f), TextAlignmentOptions.Center,
                                     new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 60));

            var warnRt = UIBuild.Rect("Warning", root, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 100));
            var warnBack = UIBuild.Image("Back", warnRt, a.white, new Color(0.25f, 0f, 0.1f, 0.55f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 90));
            warning = warnRt.gameObject.AddComponent<CanvasGroup>(); warning.alpha = 0f; warning.blocksRaycasts = false;
            warningText = UIBuild.Text("Text", warnRt, "", 46, new Color(1f, 0.75f, 0.8f), TextAlignmentOptions.Center,
                                       new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 90), true);

            // ---------------- Dialogue
            var dlg = UIBuild.Rect("Dialogue", root, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(60, -200), new Vector2(1500, 230));
            dialogue = dlg.gameObject.AddComponent<CanvasGroup>(); dialogue.alpha = 0f; dialogue.blocksRaycasts = false;
            UIBuild.Image("Back", dlg, a.white, new Color(0.03f, 0.04f, 0.08f, 0.82f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 230));
            UIBuild.Image("Line", dlg, a.white, new Color(0.55f, 0.75f, 1f, 0.7f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1500, 3));
            dialoguePortrait = UIBuild.Image("Portrait", dlg, a.guidePortrait, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(180, 180), true);
            dialogueName = UIBuild.Text("Name", dlg, "", 34, new Color(0.7f, 0.85f, 1f), TextAlignmentOptions.TopLeft,
                                        new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, -20), new Vector2(900, 46), true);
            dialogueBody = UIBuild.Text("Body", dlg, "", 36, Color.white, TextAlignmentOptions.TopLeft,
                                        new Vector2(0, 1), new Vector2(0, 1), new Vector2(240, -76), new Vector2(1200, 130));
            dialoguePrompt = UIBuild.Text("Prompt", dlg, "[Space]", 22, new Color(1, 1, 1, 0.6f), TextAlignmentOptions.BottomRight,
                                          new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 16), new Vector2(300, 30));

            // ---------------- Panels
            pausePanel = Panel(root, "일시정지", new[] { ("계속하기", (Action)TogglePause), ("스테이지 다시 시작", (Action)(() => { TogglePause(); GameFlow.RestartStage(); })), ("타이틀로", (Action)(() => { TogglePause(); GameFlow.Load(GameFlow.Title); })) });
            gameOverPanel = Panel(root, "쓰러졌습니다", new[] { ("다시 도전", (Action)GameFlow.RestartStage), ("타이틀로", (Action)(() => GameFlow.Load(GameFlow.Title))) });
            victoryPanel = Panel(root, "", new[] { ("타이틀로", (Action)(() => GameFlow.Load(GameFlow.Title))) });
            victoryText = victoryPanel.transform.Find("Title").GetComponent<TextMeshProUGUI>();
        }

        GameObject Panel(Transform root, string title, (string, Action)[] buttons)
        {
            var p = UIBuild.Stretch("Panel_" + title, root);
            var bg = p.gameObject.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0.02f, 0.72f);
            var t = UIBuild.Text("Title", p, title, 80, Color.white, TextAlignmentOptions.Center,
                                 new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1600, 260), true);
            t.outlineWidth = 0.1f;
            for (int i = 0; i < buttons.Length; i++)
                UIBuild.Button("Btn" + i, p, buttons[i].Item1, new Vector2(0, -40 - i * 100), new Vector2(460, 80), buttons[i].Item2);
            p.gameObject.SetActive(false);
            return p.gameObject;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            es.transform.SetParent(null);
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                var h = pc.Health;
                shownHp = Mathf.MoveTowards(shownHp, h.Ratio, Time.unscaledDeltaTime * 1.5f);
                hpFill.fillAmount = shownHp;
                hpText.text = $"{h.Current} / {h.max}";
                var combat = pc.GetComponent<PlayerCombat>();
                for (int i = 0; i < 3; i++)
                {
                    bool sel = combat.weaponIndex == i;
                    weaponSlots[i].transform.localScale = Vector3.one * (sel ? 1.12f : 0.92f);
                    weaponSlots[i].color = sel ? Color.white : new Color(1, 1, 1, 0.55f);
                    weaponIcons[i].color = sel ? Color.white : new Color(1, 1, 1, 0.45f);
                }
                var w = combat.Weapon;
                for (int i = 0; i < 2; i++)
                {
                    float ratio = combat.CooldownRatio(i);
                    float rem = combat.CooldownRemaining(i);
                    skillCooldown[i].fillAmount = ratio;
                    skillTimers[i].text = rem > 0.05f ? Mathf.CeilToInt(rem).ToString() : "";
                    skillNames[i].text = i == 0 ? w.skill1Name : w.skill2Name;
                }
            }

            if (bossHealth != null)
                bossFill.fillAmount = Mathf.MoveTowards(bossFill.fillAmount, bossHealth.Ratio, Time.unscaledDeltaTime * 0.8f);

            if (GameInput.PausePressed && !gameOverPanel.activeSelf && !victoryPanel.activeSelf && !DialogueOpen &&
                (pc == null || !pc.IsDead) && !ScreenFader.Instance.Busy)
                TogglePause();
        }

        // ---------------------------------------------------------------- API

        public void SetHudVisible(bool v) => hud.gameObject.SetActive(v);

        public void TogglePause()
        {
            Paused = !Paused;
            pausePanel.SetActive(Paused);
            Time.timeScale = Paused ? 0f : 1f;
            GameInput.Locked = Paused || DialogueOpen;
        }

        public void ShowGameOver() => gameOverPanel.SetActive(true);

        public void ShowVictory(string text)
        {
            victoryText.text = text;
            victoryPanel.SetActive(true);
        }

        public void ShowBoss(string displayName, Health health)
        {
            bossHealth = health;
            bossName.text = displayName;
            bossFill.fillAmount = 0f;
            bossBar.gameObject.SetActive(true);
        }

        public void HideBoss() => bossBar.gameObject.SetActive(false);

        public void ShowBanner(string title, string sub, float hold = 2.2f) => StartCoroutine(BannerRoutine(title, sub, hold));

        IEnumerator BannerRoutine(string title, string sub, float hold)
        {
            bannerTitle.text = title; bannerSub.text = sub;
            yield return FadeGroup(banner, 0f, 1f, 0.6f);
            yield return new WaitForSecondsRealtime(hold);
            yield return FadeGroup(banner, 1f, 0f, 0.8f);
        }

        public void ShowWarning(string text, float hold = 2.2f)
        {
            StopCoroutine(nameof(WarningRoutine));
            StartCoroutine(WarningRoutine(text, hold));
        }

        IEnumerator WarningRoutine(string text, float hold)
        {
            warningText.text = text;
            yield return FadeGroup(warning, warning.alpha, 1f, 0.3f);
            yield return new WaitForSecondsRealtime(hold);
            yield return FadeGroup(warning, 1f, 0f, 0.5f);
        }

        static IEnumerator FadeGroup(CanvasGroup g, float from, float to, float dur)
        {
            for (float t = 0; t < dur; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            g.alpha = to;
        }

        /// <summary>Show dialogue lines one by one (typewriter). Locks gameplay input until finished.</summary>
        public IEnumerator Dialogue(string speaker, IList<string> lines, Sprite portrait = null)
        {
            DialogueOpen = true;
            GameInput.Locked = true;
            Time.timeScale = 0f;          // the world waits while the guide talks
            dialogueName.text = speaker;
            dialoguePortrait.sprite = portrait != null ? portrait : GameAssets.I.guidePortrait;
            dialoguePortrait.enabled = dialoguePortrait.sprite != null;
            dialogueBody.rectTransform.anchoredPosition = new Vector2(dialoguePortrait.enabled ? 240 : 50, -76);
            dialogueName.rectTransform.anchoredPosition = new Vector2(dialoguePortrait.enabled ? 240 : 50, -20);
            yield return FadeGroup(dialogue, 0f, 1f, 0.25f);
            foreach (var line in lines)
            {
                dialogueBody.text = line;
                dialogueBody.maxVisibleCharacters = 0;
                dialoguePrompt.enabled = false;
                yield return null;
                int total = line.Length;
                float t = 0f;
                while (dialogueBody.maxVisibleCharacters < total)
                {
                    if (GameInput.AdvancePressed) { dialogueBody.maxVisibleCharacters = total; yield return null; break; }
                    t += Time.unscaledDeltaTime;
                    dialogueBody.maxVisibleCharacters = Mathf.Min(total, Mathf.FloorToInt(t / 0.035f));
                    yield return null;
                }
                dialoguePrompt.enabled = true;
                while (!GameInput.AdvancePressed) yield return null;
                AudioHub.Sfx(GameAssets.I.sfxUiClick, 0.4f, 0f);
                yield return null;
            }
            yield return FadeGroup(dialogue, 1f, 0f, 0.2f);
            yield return null;   // swallow the key press that closed the box
            DialogueOpen = false;
            GameInput.Locked = Paused;
            if (!Paused) Time.timeScale = 1f;
        }
    }
}
