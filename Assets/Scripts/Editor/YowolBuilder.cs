#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Yowol.EditorTools
{
    /// <summary>
    /// Builds the whole game content from the normalized art: sprite import, animation libraries, FX,
    /// weapon data, prefabs and the Intro/Stage1/Stage2/BossStage scenes.
    /// Menu: Yowol/Build All  (each step can also be run on its own).
    /// </summary>
    public static class YowolBuilder
    {
        const string CharRoot = "Assets/Art/Characters";
        const string DataRoot = "Assets/Data";
        const string PrefabRoot = "Assets/Prefabs/Game";
        const string SceneRoot = "Assets/Scenes";
        const string LegacyScene = "Assets/Legacy/MainStage_Legacy.unity";
        const string UrpMat = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/";

        [MenuItem("Yowol/Build All")]
        public static string BuildAll()
        {
            var log = new List<string>();
            log.Add(ImportCharacters());
            log.Add(ImportFx());
            log.Add(MakeGeneratedSprites());
            log.Add(MakeWeapons());
            log.Add(MakePrefabs());
            log.Add(MakeGameAssets());
            log.Add(BuildScenes());
            return string.Join("\n", log);
        }

        // ------------------------------------------------------------------ helpers

        static void EnsureFolder(string path)
        {
            path = path.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            EnsureFolder(Path.GetDirectoryName(path));
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        [System.Serializable] class AtlasFrame { public int page, x, y, w, h; public float px, py; }
        [System.Serializable] class AtlasClip { public bool loop; public float[] durations; public AtlasFrame[] frames; }

        /// <summary>Slice one texture into named sprite rects with custom pivots.</summary>
        static void Slice(string texPath, List<(string name, Rect rect, Vector2 pivot)> rects, float ppu, int maxSize = 4096)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            if (importer == null) { Debug.LogError("No texture at " + texPath); return; }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();
            var existing = dp.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
            var list = new List<SpriteRect>();
            foreach (var (name, rect, pivot) in rects)
            {
                list.Add(new SpriteRect
                {
                    name = name, rect = rect, alignment = SpriteAlignment.Custom, pivot = pivot,
                    spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate()
                });
            }
            dp.SetSpriteRects(list.ToArray());
            var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            ids?.SetNameFileIdPairs(list.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            importer.SaveAndReimport();
        }

        static Dictionary<string, Sprite> SpritesAt(string texPath) =>
            AssetDatabase.LoadAllAssetsAtPath(texPath).OfType<Sprite>().ToDictionary(s => s.name, s => s);

        // ------------------------------------------------------------------ characters

        [MenuItem("Yowol/Steps/1 Import Characters")]
        public static string ImportCharacters()
        {
            var report = new List<string>();
            foreach (var who in new[] { "Player", "Boss", "Reaper" })
            {
                string dir = $"{CharRoot}/{who}";
                string jsonPath = $"{dir}/{who}_atlas.json";
                var text = File.ReadAllText(jsonPath);
                var root = MiniJson.Parse(text) as Dictionary<string, object>;
                float ppu = System.Convert.ToSingle(root["ppu"]);
                var pages = ((List<object>)root["pages"]).Select(p => (string)p).ToList();
                var clips = (Dictionary<string, object>)root["clips"];

                var perPage = pages.Select(_ => new List<(string, Rect, Vector2)>()).ToList();
                foreach (var kv in clips)
                {
                    var frames = (List<object>)((Dictionary<string, object>)kv.Value)["frames"];
                    for (int i = 0; i < frames.Count; i++)
                    {
                        var f = (Dictionary<string, object>)frames[i];
                        int page = System.Convert.ToInt32(f["page"]);
                        var rect = new Rect(System.Convert.ToSingle(f["x"]), System.Convert.ToSingle(f["y"]),
                                            System.Convert.ToSingle(f["w"]), System.Convert.ToSingle(f["h"]));
                        var pivot = new Vector2(System.Convert.ToSingle(f["px"]), System.Convert.ToSingle(f["py"]));
                        perPage[page].Add(($"{kv.Key}_{i:00}", rect, pivot));
                    }
                }
                var sprites = new Dictionary<string, Sprite>();
                for (int p = 0; p < pages.Count; p++)
                {
                    string tex = $"{dir}/{pages[p]}";
                    Slice(tex, perPage[p], ppu);
                    foreach (var kv in SpritesAt(tex)) sprites[kv.Key] = kv.Value;
                }

                var lib = LoadOrCreate<SpriteAnimLibrary>($"{DataRoot}/Anim/{who}_Anim.asset");
                lib.clips.Clear();
                foreach (var kv in clips)
                {
                    var c = (Dictionary<string, object>)kv.Value;
                    var frames = (List<object>)c["frames"];
                    var durs = ((List<object>)c["durations"]).Select(d => System.Convert.ToSingle(d)).ToArray();
                    lib.clips.Add(new SpriteAnimLibrary.Clip
                    {
                        name = kv.Key,
                        loop = (bool)c["loop"],
                        durations = durs,
                        frames = Enumerable.Range(0, frames.Count).Select(i => sprites.TryGetValue($"{kv.Key}_{i:00}", out var s) ? s : null).ToArray()
                    });
                }
                EditorUtility.SetDirty(lib);
                report.Add($"{who}: {lib.clips.Count} clips, {sprites.Count} sprites");
            }
            AssetDatabase.SaveAssets();
            return "Characters: " + string.Join("; ", report);
        }

        // ------------------------------------------------------------------ FX

        struct FxSheet
        {
            public string path, clip; public int cols, rows, count; public float frame, ppu; public bool loop;
            public FxSheet(string path, string clip, int cols, int rows, int count, float frame, float ppu, bool loop)
            { this.path = path; this.clip = clip; this.cols = cols; this.rows = rows; this.count = count; this.frame = frame; this.ppu = ppu; this.loop = loop; }
        }

        static readonly FxSheet[] FxSheets =
        {
            new FxSheet("Sword/Skill_0/Sword_Skill_0_1st.png", "SwordSlash1", 4, 2, 7, 0.035f, 200, false),
            new FxSheet("Sword/Skill_0/Sword_Skill_0_2st.png", "SwordSlash2", 4, 2, 7, 0.035f, 220, false),
            new FxSheet("Sword/Skill_0/Sword_Skill_0_3st.png", "SwordSlash3", 4, 2, 7, 0.035f, 220, false),
            new FxSheet("Sword/Sword_Hit_Effect/Sword_Hit_Effect.png", "SwordHit", 4, 2, 7, 0.035f, 256, false),
            new FxSheet("Sword/Skill_1/Sword_Skill_2.png", "SwordFlurry", 4, 4, 15, 0.04f, 170, true),
            new FxSheet("Sword/Skill_1/Sword_Skill_1.png", "SwordSpark", 2, 2, 4, 0.05f, 256, false),
            new FxSheet("Sword/Skill_2/Sword_Skill_2.png", "MoonSlash", 4, 4, 14, 0.05f, 200, true),
            new FxSheet("Bow/Bow_Skill_0/projectile_sheet.png", "Arrow", 4, 2, 8, 0.05f, 160, true),
            new FxSheet("Bow/Bow_Skill_1/Bow_Skill_1.png", "ArrowRain", 4, 2, 8, 0.07f, 200, true),
            new FxSheet("Bow/Bow_Skill_1/mint_ring_loop_sheet.png", "MintRing", 4, 1, 4, 0.09f, 150, true),
            new FxSheet("Bow/Bow_Skill_2/bow_appear_sheet_4x4.png", "BowAppear", 4, 4, 15, 0.05f, 100, false),
            new FxSheet("Bow/Bow_Skill_2/projectile_sheet_2x4.png", "BowBeam", 2, 4, 8, 0.04f, 200, true),
            new FxSheet("L_Sword/L_Swrod_Skill_0/L_Sword_Skill_0.png", "GreatSlash", 4, 2, 8, 0.035f, 170, false),
            new FxSheet("L_Sword/L_Swrod_Skill_1/Cross_Wave_sheet.png", "CrossWave", 4, 2, 7, 0.045f, 200, false),
        };

        [MenuItem("Yowol/Steps/2 Import FX")]
        public static string ImportFx()
        {
            var lib = LoadOrCreate<SpriteAnimLibrary>($"{DataRoot}/Anim/FX_Anim.asset");
            lib.clips.Clear();
            foreach (var s in FxSheets)
            {
                string path = "Assets/Art/FX/Skill/" + s.path;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { Debug.LogWarning("FX missing " + path); continue; }
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.GetSourceTextureWidthAndHeight(out int w, out int h);
                float cw = w / (float)s.cols, ch = h / (float)s.rows;
                var rects = new List<(string, Rect, Vector2)>();
                for (int i = 0; i < s.count; i++)
                {
                    int cx = i % s.cols, cy = i / s.cols;
                    var r = new Rect(Mathf.Floor(cx * cw), Mathf.Floor(h - (cy + 1) * ch), Mathf.Floor(cw), Mathf.Floor(ch));
                    rects.Add(($"{s.clip}_{i:00}", r, new Vector2(0.5f, 0.5f)));
                }
                Slice(path, rects, s.ppu, 8192);
                var sprites = SpritesAt(path);
                var frames = Enumerable.Range(0, s.count).Select(i => sprites[$"{s.clip}_{i:00}"]).ToArray();
                lib.clips.Add(new SpriteAnimLibrary.Clip { name = s.clip, loop = s.loop, frames = frames, durations = Enumerable.Repeat(s.frame, s.count).ToArray() });
                if (s.clip == "GreatSlash")
                    lib.clips.Add(new SpriteAnimLibrary.Clip { name = "GreatSlam", loop = false, frames = frames, durations = Enumerable.Repeat(0.05f, s.count).ToArray() });
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return $"FX: {lib.clips.Count} clips";
        }

        // ------------------------------------------------------------------ generated sprites

        [MenuItem("Yowol/Steps/3 Generated Sprites")]
        public static string MakeGeneratedSprites()
        {
            EnsureFolder("Assets/Art/Generated");
            WriteTex("Assets/Art/Generated/soft_circle.png", 128, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f)) / 64f;
                float a = Mathf.Clamp01(1f - d);
                return new Color(1, 1, 1, a * a);
            }, 128);
            WriteTex("Assets/Art/Generated/white.png", 8, (x, y) => Color.white, 8);
            return "Generated sprites";
        }

        static void WriteTex(string path, int size, System.Func<int, int, Color> f, float ppu)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) t.SetPixel(x, y, f(x, y));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = ppu;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ weapons

        [MenuItem("Yowol/Steps/4 Weapons")]
        public static string MakeWeapons()
        {
            var sword = LoadOrCreate<WeaponData>($"{DataRoot}/Weapons/Sword.asset");
            sword.id = WeaponId.Sword; sword.displayName = "검";
            sword.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Ingame/Sword.png");
            sword.idleClip = "Sword_Idle"; sword.attackClip = "Sword_Attack"; sword.jumpClip = "Sword_Jump"; sword.jumpInPlaceClip = "Sword_JumpInPlace";
            sword.damage = 15; sword.cooldown = 0.28f; sword.hitFrame = 1; sword.hitOffset = new Vector2(1.3f, 1.0f); sword.hitSize = new Vector2(2.5f, 1.9f);
            sword.knockback = 3f; sword.lungeSpeed = 2.5f; sword.moveWhileAttacking = 0.25f;
            sword.comboFx = new[] { "SwordSlash1", "SwordSlash2", "SwordSlash3" }; sword.fxScale = 1f; sword.fxOffset = new Vector2(1.2f, 1.0f);
            sword.skill1Name = "난무"; sword.skill1Cooldown = 18f; sword.skill1Hits = 10; sword.skill1Damage = 10;
            sword.skill2Name = "월광참"; sword.skill2Cooldown = 45f; sword.skill2Hits = 6; sword.skill2Damage = 38;

            var bow = LoadOrCreate<WeaponData>($"{DataRoot}/Weapons/Bow.asset");
            bow.id = WeaponId.Bow; bow.displayName = "활";
            bow.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Public/Bow.png");
            bow.idleClip = "Bow_Idle"; bow.attackClip = "Bow_Attack"; bow.jumpClip = "Bow_Jump"; bow.jumpInPlaceClip = "Bow_JumpInPlace";
            bow.damage = 25; bow.cooldown = 0.3f; bow.hitFrame = 2; bow.knockback = 2f; bow.lungeSpeed = 0f; bow.moveWhileAttacking = 0.1f;
            bow.comboFx = new string[0];
            bow.skill1Name = "화살비"; bow.skill1Cooldown = 14f; bow.skill1Hits = 21; bow.skill1Damage = 10;
            bow.skill2Name = "월궁"; bow.skill2Cooldown = 40f; bow.skill2Hits = 12; bow.skill2Damage = 20;

            var gs = LoadOrCreate<WeaponData>($"{DataRoot}/Weapons/GreatSword.asset");
            gs.id = WeaponId.GreatSword; gs.displayName = "대검";
            gs.icon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Public/Dual_Sword.png");
            gs.idleClip = "GreatSword_Idle"; gs.attackClip = "GreatSword_Attack"; gs.jumpClip = "GreatSword_Jump"; gs.jumpInPlaceClip = "GreatSword_JumpInPlace";
            gs.damage = 30; gs.cooldown = 0.45f; gs.hitFrame = 2; gs.hitOffset = new Vector2(1.5f, 1.1f); gs.hitSize = new Vector2(3.0f, 2.4f);
            gs.knockback = 5f; gs.lungeSpeed = 1.5f; gs.moveWhileAttacking = 0f;
            gs.comboFx = new[] { "GreatSlash" }; gs.fxScale = 1.1f; gs.fxOffset = new Vector2(1.3f, 1.0f);
            gs.skill1Name = "반격"; gs.skill1Cooldown = 1.5f;
            gs.skill2Name = "대지 분쇄"; gs.skill2Cooldown = 6f; gs.skill2Hits = 3; gs.skill2Damage = 100;

            foreach (var w in new[] { sword, bow, gs }) EditorUtility.SetDirty(w);
            AssetDatabase.SaveAssets();
            return "Weapons: Sword, Bow, GreatSword";
        }

        // ------------------------------------------------------------------ prefabs

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(UrpMat + name);

        static PhysicsMaterial2D NoFriction()
        {
            string path = $"{DataRoot}/Physics/NoFriction.physicsMaterial2D";
            var m = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (m != null) return m;
            EnsureFolder($"{DataRoot}/Physics");
            m = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static (GameObject root, SpriteAnimator anim) Character(string name, int layer, string lib, string defaultClip, int order)
        {
            var root = new GameObject(name) { layer = layer };
            var visual = new GameObject("Visual") { layer = layer };
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = Mat("Sprite-Lit-Default.mat");
            sr.sortingOrder = order;
            var anim = visual.AddComponent<SpriteAnimator>();
            anim.library = AssetDatabase.LoadAssetAtPath<SpriteAnimLibrary>($"{DataRoot}/Anim/{lib}_Anim.asset");
            anim.defaultClip = defaultClip;
            var c = anim.library.Get(defaultClip);
            if (c != null) sr.sprite = c.frames[0];
            return (root, anim);
        }

        static Light2D AddPointLight(Transform parent, Vector3 local, Color color, float radius, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.falloffIntensity = 0.7f;
            return l;
        }

        [MenuItem("Yowol/Steps/5 Prefabs")]
        public static string MakePrefabs()
        {
            EnsureFolder(PrefabRoot);
            var noFriction = NoFriction();

            // Player
            var (player, panim) = Character("Player", Layers.Player, "Player", "Sword_Idle", 20);
            player.tag = "Player";
            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3.2f; rb.freezeRotation = true; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.sharedMaterial = noFriction;
            var cap = player.AddComponent<CapsuleCollider2D>();
            cap.size = new Vector2(0.7f, 1.75f); cap.offset = new Vector2(0f, 0.9f); cap.sharedMaterial = noFriction;
            var hp = player.AddComponent<Health>(); hp.max = 100; hp.showDamageNumbers = true; hp.damageNumberOffset = new Vector2(0, 2.3f);
            var flash = player.AddComponent<HitFlash>(); flash.blinkWhileInvulnerable = true;
            var pc = player.AddComponent<PlayerController>();
            pc.visual = panim.transform; pc.anim = panim;
            var combat = player.AddComponent<PlayerCombat>();
            combat.weapons = new[]
            {
                AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataRoot}/Weapons/Sword.asset"),
                AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataRoot}/Weapons/Bow.asset"),
                AssetDatabase.LoadAssetAtPath<WeaponData>($"{DataRoot}/Weapons/GreatSword.asset"),
            };
            AddPointLight(player.transform, new Vector3(0, 1.1f, 0), new Color(0.75f, 0.85f, 1f), 3.5f, 0.55f);
            PrefabUtility.SaveAsPrefabAsset(player, $"{PrefabRoot}/Player.prefab");
            Object.DestroyImmediate(player);

            // Reaper
            var (reaper, ranim) = Character("Reaper", Layers.Enemy, "Reaper", "Reaper_Walk", 19);
            var rrb = reaper.AddComponent<Rigidbody2D>();
            rrb.gravityScale = 3f; rrb.freezeRotation = true; rrb.interpolation = RigidbodyInterpolation2D.Interpolate; rrb.sharedMaterial = noFriction;
            var rcap = reaper.AddComponent<CapsuleCollider2D>();
            rcap.size = new Vector2(1.0f, 1.7f); rcap.offset = new Vector2(0f, 0.85f); rcap.sharedMaterial = noFriction;
            reaper.AddComponent<Health>().damageNumberOffset = new Vector2(0, 2.2f);
            reaper.AddComponent<HitFlash>();
            var re = reaper.AddComponent<ReaperEnemy>();
            re.visual = ranim.transform; re.anim = ranim; re.maxHp = 120; re.contactDamage = 0;
            PrefabUtility.SaveAsPrefabAsset(reaper, $"{PrefabRoot}/Reaper.prefab");
            Object.DestroyImmediate(reaper);

            // Boss
            var (boss, banim) = Character("Boss_SoulWitch", Layers.Enemy, "Boss", "Boss_Float", 19);
            var brb = boss.AddComponent<Rigidbody2D>();
            brb.bodyType = RigidbodyType2D.Kinematic; brb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = boss.AddComponent<BoxCollider2D>();
            box.isTrigger = true; box.size = new Vector2(2.4f, 3.3f); box.offset = new Vector2(0f, -1.3f);
            boss.AddComponent<Health>();
            boss.AddComponent<HitFlash>();
            var bw = boss.AddComponent<BossWitch>();
            bw.visual = banim.transform; bw.anim = banim; bw.maxHp = 1600; bw.contactDamage = 10; bw.countsForStageClear = false;
            AddPointLight(boss.transform, new Vector3(0, -1.2f, 0), new Color(0.62f, 0.4f, 1f), 5f, 1.1f);
            PrefabUtility.SaveAsPrefabAsset(boss, $"{PrefabRoot}/Boss_SoulWitch.prefab");
            Object.DestroyImmediate(boss);

            AssetDatabase.SaveAssets();
            return "Prefabs: Player, Reaper, Boss_SoulWitch";
        }

        // ------------------------------------------------------------------ shared assets

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning($"[YowolBuilder] missing {typeof(T).Name} at {path}");
            return a;
        }

        static Sprite SpriteAt(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && imp.textureType != TextureImporterType.Sprite)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.SaveAndReimport();
            }
            var s = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (s == null) Debug.LogWarning("[YowolBuilder] no sprite at " + path);
            return s;
        }

        [MenuItem("Yowol/Steps/6 Game Assets")]
        public static string MakeGameAssets()
        {
            EnsureFolder("Assets/Resources");
            var a = LoadOrCreate<GameAssets>("Assets/Resources/GameAssets.asset");
            a.bodyFont = Load<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/NewHangulFont SDF.asset");
            a.titleFont = Load<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/KERISBAEUM_B SDF.asset") ?? a.bodyFont;
            a.fx = Load<SpriteAnimLibrary>($"{DataRoot}/Anim/FX_Anim.asset");
            a.softCircle = SpriteAt("Assets/Art/Generated/soft_circle.png");
            a.white = SpriteAt("Assets/Art/Generated/white.png");
            a.litSprite = Mat("Sprite-Lit-Default.mat");
            a.unlitSprite = Mat("Sprite-Unlit-Default.mat");
            a.hpFrame = SpriteAt("Assets/Art/Sprites/Public/HP_Bar_Background.png");
            a.hpFill = SpriteAt("Assets/Art/Sprites/Ingame/HP_Bar.png");
            a.gaugeFill = SpriteAt("Assets/Art/Sprites/Public/Gauge_Bar.png");
            a.slot = SpriteAt("Assets/Art/Sprites/Ingame/Ingame_Slot.png");
            a.weaponIcons = new[]
            {
                SpriteAt("Assets/Art/Sprites/Ingame/Sword.png"),
                SpriteAt("Assets/Art/Sprites/Public/Bow.png"),
                SpriteAt("Assets/Art/Sprites/Public/Dual_Sword.png"),
            };
            a.guidePortrait = SpriteAt("Assets/Art/Sprites/Aspic_Eyes.png");
            a.playerPrefab = Load<GameObject>($"{PrefabRoot}/Player.prefab");
            a.reaperPrefab = Load<GameObject>($"{PrefabRoot}/Reaper.prefab");

            const string S = "Assets/Audio/SFX/";
            a.sfxSwing = Load<AudioClip>(S + "Combat/sword.wav");
            a.sfxHeavySwing = a.sfxSwing;
            a.sfxBow = Load<AudioClip>(S + "Combat/beam.wav");
            a.sfxHit = Load<AudioClip>(S + "Combat/sword.wav");
            a.sfxPlayerHurt = Load<AudioClip>("Assets/Audio/Voice/wav/mai_free_08_damage.wav");
            a.sfxJump = Load<AudioClip>("Assets/Audio/Voice/wav/mai_free_03_jump.wav");
            a.sfxFootstep = Load<AudioClip>(S + "Combat/footstep.wav");
            a.sfxDeath = Load<AudioClip>("Assets/Audio/Voice/wav/mai_free_07_ouch.wav");
            a.sfxEnemyDeath = Load<AudioClip>(S + "Combat/DeathExplosion.wav");
            a.sfxBossCast = Load<AudioClip>(S + "Combat/power_load.wav");
            a.sfxBossBeam = Load<AudioClip>(S + "Combat/beam.wav");
            a.sfxBossHurt = Load<AudioClip>(S + "Combat/pain.wav");
            a.sfxExplosion = Load<AudioClip>(S + "Combat/DeathExplosion.wav");
            a.sfxUiClick = Load<AudioClip>(S + "UI/SFX_UI_Button_Organic_Plastic_Thin_Select_1.wav");
            a.sfxSkill = Load<AudioClip>(S + "Combat/power_load.wav");
            a.voiceAttack = new[] { Load<AudioClip>("Assets/Audio/Voice/wav/mai_free_01_attack.wav"), Load<AudioClip>("Assets/Audio/Voice/wav/mai_free_02_attack2.wav") };
            a.voiceHurt = new[] { Load<AudioClip>("Assets/Audio/Voice/wav/mai_free_07_ouch.wav") };
            a.bgmStage = Load<AudioClip>("Assets/Audio/BGM/Intro_1.mp3");
            a.bgmBoss = Load<AudioClip>("Assets/Audio/BGM/Intro_0.mp3");
            a.bgmIntro = Load<AudioClip>("Assets/Audio/BGM/Intro_0.mp3");
            a.ambienceRain = Load<AudioClip>("Assets/Audio/Ambience/Rain (2) Loop.wav");
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            return "GameAssets ready";
        }

        // ------------------------------------------------------------------ scenes

        [MenuItem("Yowol/Steps/7 Scenes")]
        public static string BuildScenes()
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
            var report = new List<string>
            {
                BuildIntro(),
                BuildStage1(),
                BuildStage2(),
                BuildBossStage(),
            };
            EditorBuildSettings.scenes = new[] { "Title", "Intro", "Stage1", "Stage2", "BossStage" }
                .Select(n => new EditorBuildSettingsScene($"{SceneRoot}/{n}.unity", true)).ToArray();
            report.Add("Build list: Title > Intro > Stage1 > Stage2 > BossStage");
            return string.Join("\n", report);
        }

        static Scene NewScene(string name)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;
            return scene;
        }

        static Camera MakeCamera(float size, Color bg)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.transform.position = new Vector3(0, 0, -10);
            go.AddComponent<AudioListener>();
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return cam;
        }

        static VolumeProfile GameVolumeProfile()
        {
            string path = "Assets/Settings/GameVolume.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var bloom = p.Add<Bloom>(true);
            bloom.intensity.Override(0.7f); bloom.threshold.Override(0.85f); bloom.scatter.Override(0.65f);
            var vig = p.Add<Vignette>(true);
            vig.intensity.Override(0.28f); vig.smoothness.Override(0.5f);
            var ca = p.Add<ColorAdjustments>(true);
            ca.contrast.Override(8f); ca.saturation.Override(4f);
            foreach (var c in p.components) AssetDatabase.AddObjectToAsset(c, p);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static void AddVolume()
        {
            var go = new GameObject("Global Volume");
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = GameVolumeProfile();
        }

        static Light2D AddGlobalLight(Color color, float intensity)
        {
            var go = new GameObject("Global Light 2D");
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Global;
            l.color = color;
            l.intensity = intensity;
            return l;
        }

        static void Save(Scene scene, string name)
        {
            EnsureFolder(SceneRoot);
            EditorSceneManager.SaveScene(scene, $"{SceneRoot}/{name}.unity");
        }

        static GameObject Empty(string name, Vector3 pos, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go;
        }

        static void Wall(string name, Vector2 center, Vector2 size)
        {
            var go = Empty(name, center);
            go.layer = Layers.Ground;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        static void Kill(Vector2 center, Vector2 size)
        {
            var go = Empty("KillZone", center);
            var b = go.AddComponent<BoxCollider2D>(); b.isTrigger = true; b.size = size;
            go.AddComponent<KillZone>();
        }

        static StageExit Exit(Vector2 groundPoint, string[] lines)
        {
            var go = Empty("StageExit", groundPoint);
            var b = go.AddComponent<BoxCollider2D>(); b.isTrigger = true; b.size = new Vector2(1.6f, 4f); b.offset = new Vector2(0, 2f);
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(go.transform, false);
            glowGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            glowGo.transform.localScale = new Vector3(2.2f, 4.2f, 1f);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = SpriteAt("Assets/Art/Generated/soft_circle.png");
            glow.sharedMaterial = Mat("Sprite-Unlit-Default.mat");
            glow.color = new Color(0.55f, 0.85f, 1f, 0.7f);
            glow.sortingOrder = 18;
            var exit = go.AddComponent<StageExit>();
            exit.glow = glow;
            exit.lightSource = AddPointLight(go.transform, new Vector3(0, 1.6f, 0), new Color(0.55f, 0.85f, 1f), 4f, 1.4f);
            exit.enterLines = lines;
            return exit;
        }

        static void Trigger(Vector2 center, Vector2 size, string[] lines)
        {
            var go = Empty("GuideTrigger", center);
            var b = go.AddComponent<BoxCollider2D>(); b.isTrigger = true; b.size = size;
            go.AddComponent<GuideTrigger>().lines = lines;
        }

        static void SpawnReapers(float groundY, params float[] xs)
        {
            var prefab = Load<GameObject>($"{PrefabRoot}/Reaper.prefab");
            var parent = Empty("Enemies", Vector3.zero);
            foreach (var x in xs)
            {
                var r = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.scene);
                r.transform.SetParent(parent.transform, true);
                r.transform.position = new Vector3(x, groundY + 0.05f, 0);
            }
        }

        static GameObject GuideSpiritObject(Vector3 pos)
        {
            var go = Empty("Guide_GrayFire", pos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteAt("Assets/Art/Sprites/Aspic_Eyes.png");
            sr.sharedMaterial = Mat("Sprite-Unlit-Default.mat");
            sr.sortingOrder = 21;
            float h = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
            go.transform.localScale = Vector3.one * (0.9f / Mathf.Max(0.01f, h));
            go.AddComponent<GuideSpirit>();
            var l = AddPointLight(go.transform, Vector3.zero, new Color(0.8f, 0.85f, 1f), 2.5f, 0.6f);
            l.transform.localScale = Vector3.one;
            return go;
        }

        /// <summary>Copy root objects from the legacy main stage (environment only; missing scripts stripped).</summary>
        static List<GameObject> CopyLegacy(Scene target, System.Func<GameObject, bool> filter)
        {
            var legacy = EditorSceneManager.OpenScene(LegacyScene, OpenSceneMode.Additive);
            var copies = new List<GameObject>();
            foreach (var root in legacy.GetRootGameObjects())
            {
                if (!filter(root)) continue;
                var copy = Object.Instantiate(root);
                copy.name = root.name;
                SceneManager.MoveGameObjectToScene(copy, target);
                foreach (var t in copy.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var v in copy.GetComponentsInChildren<Volume>(true)) Object.DestroyImmediate(v.gameObject);
                foreach (var psr in copy.GetComponentsInChildren<ParticleSystemRenderer>(true))
                    if (psr.sharedMaterial == null || psr.sharedMaterial.shader == null || !psr.sharedMaterial.shader.name.Contains("Universal"))
                        psr.sharedMaterial = Mat("ParticlesUnlit.mat");
                copies.Add(copy);
            }
            EditorSceneManager.CloseScene(legacy, true);
            return copies;
        }

        static readonly HashSet<string> NotEnvironment = new HashSet<string>
        {
            "Main Camera", "Canvas", "EventSystem", "Player", "Boss", "Monster", "Managers", "TutorialMarker", "TutorialMarker (1)",
            "Stage1_Mark", "Stage2_Mark", "Boundary", "Guide_GrayFire", "Object"
        };

        /// <summary>The legacy "Light" root was disabled. Enable it with a sane global light.</summary>
        static void SetupLegacyLights(IEnumerable<GameObject> copies, float globalIntensity, bool keepPointLights)
        {
            bool hasGlobal = false;
            foreach (var c in copies.Where(c => c.name == "Light"))
            {
                c.SetActive(true);
                foreach (var l in c.GetComponentsInChildren<Light2D>(true))
                {
                    if (l.lightType == Light2D.LightType.Global)
                    {
                        l.gameObject.SetActive(true);
                        l.intensity = globalIntensity;
                        l.color = new Color(0.86f, 0.9f, 1f);
                        hasGlobal = true;
                    }
                    else if (!keepPointLights || l.name.StartsWith("Spot")) Object.DestroyImmediate(l.gameObject);
                    else l.gameObject.SetActive(true);
                }
            }
            if (!hasGlobal) AddGlobalLight(new Color(0.86f, 0.9f, 1f), globalIntensity);
        }

        static float RootX(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return go.transform.position.x;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            return b.center.x;
        }

        static string BuildIntro()
        {
            var scene = NewScene("Intro");
            MakeCamera(5f, Color.black);
            var go = new GameObject("IntroSequence");
            var intro = go.AddComponent<IntroSequence>();
            string C = "Assets/Art/Sprites/CutScene/";
            var bgm0 = Load<AudioClip>("Assets/Audio/BGM/Intro_0.mp3");
            var bgm1 = Load<AudioClip>("Assets/Audio/BGM/Intro_1.mp3");
            IntroSequence.Panel P(string img, string text, AudioClip bgm = null) =>
                new IntroSequence.Panel { image = SpriteAt(C + img), text = text, bgm = bgm };
            intro.panels = new[]
            {
                P("CutScene_0.png", "크읏.. 잡히겠어..!!", bgm0),
                P("CutScene_0.png", "빨리 벗어나야 돼..!"),
                P("CutScene_2 1.png", "저기 골목길.. 저기로 가자..!"),
                P("CutScene_2.png", "휴.. 겨우 따돌렸네..", bgm1),
                P("CutScene_3.png", "어..? 저건 뭐지?"),
                P("CutScene_4.png", "불..꽃?"),
                P("CutScecne_5.png", "(띠..링)"),
                P("CutScecne_5.png", "??? : 안녕하세요. 새로운 『창조자』님"),
                P("CutScecne_5.png", "넌.. 누구야?"),
                P("CutScene_6.png", "제 이름은 『잿빛의 불』"),
                P("CutScene_6.png", "저는 『1대 창조자』님의 일부입니다."),
                P("CutScene_6.png", "이 세계의 새로운 가이드입니다."),
                P("CutScene_6.png", "우선 목적지로 향하시죠. 안내해드리겠습니다."),
            };
            Save(scene, "Intro");
            return "Scene Intro: 13 panels";
        }

        static StageController MakeStageController(string title, string sub, Vector3 spawn, Rect bounds, float camSize)
        {
            var go = new GameObject("StageController");
            var sc = go.AddComponent<StageController>();
            sc.stageTitle = title;
            sc.stageSubtitle = sub;
            sc.spawnPoint = Empty("SpawnPoint", spawn).transform;
            sc.cameraBounds = bounds;
            sc.cameraSize = camSize;
            sc.cameraOffset = new Vector2(0f, camSize * 0.42f);
            sc.bgm = Load<AudioClip>("Assets/Audio/BGM/Intro_1.mp3");
            sc.ambience = Load<AudioClip>("Assets/Audio/Ambience/Rain (2) Loop.wav");
            return sc;
        }

        static string BuildStage1()
        {
            var scene = NewScene("Stage1");
            var cam = MakeCamera(5f, new Color(0.05f, 0.07f, 0.1f));
            AddVolume();
            var copies = CopyLegacy(scene, r => !NotEnvironment.Contains(r.name) && !r.name.StartsWith("Monster") && r.name != "Stage_2" && r.name != "Tile (1)" && (r.name == "Light" || RootX(r) < 60f));
            SetupLegacyLights(copies, 1.0f, true);
            foreach (var c in copies)
                if (c.name == "BackGround")
                    foreach (Transform t in c.transform)
                        if (t.name.StartsWith("Far_View")) t.gameObject.AddComponent<Parallax>().factor = 0.55f;

            const float groundY = -10.48f;
            const float minX = -65f, maxX = 46.5f;
            Wall("Wall_Left", new Vector2(minX - 0.5f, groundY + 8f), new Vector2(1f, 20f));
            Wall("Wall_Right", new Vector2(maxX + 0.5f, groundY + 8f), new Vector2(1f, 20f));
            Kill(new Vector2((minX + maxX) / 2f, groundY - 8f), new Vector2(maxX - minX + 20f, 2f));

            var sc = MakeStageController("STAGE 1", "비 내리는 옥상 골목", new Vector3(-33f, groundY + 0.2f), new Rect(minX, groundY - 2.1f, maxX - minX, 14f), 5f);
            sc.openingLines = new[]
            {
                "A, D를 이용해 앞으로 가십시오. Space로 점프할 수 있습니다.",
                "마우스 왼쪽 버튼을 누르면 공격할 수 있습니다.",
                "E, Q로 무기마다 다른 스킬을 사용할 수 있어요.",
            };
            sc.clearLines = new[] { "이 근처의 혼령은 모두 정리됐어요. 앞으로 가시죠." };
            Trigger(new Vector2(-22f, groundY + 2f), new Vector2(1.5f, 4f), new[]
            {
                "숫자키 1, 2, 3으로 무기를 바꿀 수 있습니다.",
                "검은 빠르고, 활은 멀리서, 대검은 느리지만 강력하죠.",
                "대검의 E는 반격입니다. 공격을 받는 순간에 맞춰 보세요!",
            });
            Trigger(new Vector2(-8f, groundY + 2f), new Vector2(1.5f, 4f), new[] { "앞에 혼령이 있어요. 조심하세요!" });
            SpawnReapers(groundY, -12f, -3f, 9f, 18f, 27f, 36f);
            sc.exit = Exit(new Vector2(43.5f, groundY), new[] { "혼돈의 힘이 강하게 느껴지는 곳이네요..", "이쪽으로 들어갑시다." });
            GuideSpiritObject(new Vector3(-34f, groundY + 2.4f));
            cam.transform.position = new Vector3(-33f, groundY + 3f, -10f);
            Save(scene, "Stage1");
            return $"Scene Stage1: {copies.Count} legacy roots";
        }

        static string BuildStage2()
        {
            var scene = NewScene("Stage2");
            var cam = MakeCamera(3.6f, new Color(0.04f, 0.05f, 0.08f));
            AddVolume();
            var copies = CopyLegacy(scene, r => r.name == "Stage_2" || r.name == "Tile (1)" || r.name == "Light");
            SetupLegacyLights(copies, 0.95f, false);

            const float groundY = -10.07f;
            const float minX = 61.8f, maxX = 131.6f;
            Wall("Wall_Left", new Vector2(minX - 0.5f, groundY + 6f), new Vector2(1f, 16f));
            Wall("Wall_Right", new Vector2(maxX + 0.5f, groundY + 6f), new Vector2(1f, 16f));
            Kill(new Vector2((minX + maxX) / 2f, groundY - 8f), new Vector2(maxX - minX + 20f, 2f));

            var sc = MakeStageController("STAGE 2", "버려진 건물 복도", new Vector3(minX + 3f, groundY + 0.2f), new Rect(minX, groundY - 1.5f, maxX - minX, 10f), 3.6f);
            sc.openingLines = new[] { "건물 안에도 혼령이 가득하네요.", "좁은 곳이니 활로 멀리서 공격하는 것도 방법이에요." };
            sc.clearLines = new[] { "위층에서 거대한 혼령의 기운이 느껴져요..." };
            SpawnReapers(groundY, 77f, 85f, 92f, 99f, 106f, 113f, 120f);
            sc.exit = Exit(new Vector2(maxX - 2.5f, groundY), new[] { "옥상으로 올라가죠.", "각오는 되셨나요, 창조자님?" });
            GuideSpiritObject(new Vector3(minX + 2f, groundY + 2.4f));
            cam.transform.position = new Vector3(minX + 6f, groundY + 2.5f, -10f);
            Save(scene, "Stage2");
            return $"Scene Stage2: {copies.Count} legacy roots";
        }

        static GameObject Prop(string file, Vector2 bottomCenter, float scale, int order, Transform parent, bool flip = false)
        {
            var s = SpriteAt("Assets/Art/Sprites/Boss_Stage/" + file);
            var go = new GameObject(Path.GetFileNameWithoutExtension(file));
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sharedMaterial = Mat("Sprite-Lit-Default.mat");
            sr.sortingOrder = order;
            sr.flipX = flip;
            go.transform.localScale = Vector3.one * scale;
            float h = s != null ? s.bounds.size.y * scale : 0f;
            float pivotOffset = s != null ? (s.bounds.center.y) * scale : 0f;
            go.transform.position = new Vector3(bottomCenter.x, bottomCenter.y + h / 2f - pivotOffset, 0f);
            return go;
        }

        static string BuildBossStage()
        {
            var scene = NewScene("BossStage");
            var cam = MakeCamera(6f, new Color(0.03f, 0.04f, 0.07f));
            AddVolume();
            AddGlobalLight(new Color(0.7f, 0.75f, 0.95f), 0.85f);
            var env = Empty("Environment", Vector3.zero).transform;
            const float S = 1.16f;     // background art is authored at 1920x1200 / 100 ppu
            const float groundY = 0f;
            const float halfW = 10.6f;

            var bg = Prop("00_night_city_background.png", new Vector2(0, -3.6f), S * 1.25f, -20, env);
            bg.AddComponent<Parallax>().factor = 0.0f;
            Prop("rear_parapet.png", new Vector2(0, groundY - 0.05f), S, -5, env);
            Prop("left_access_building.png", new Vector2(-8.2f, groundY), S, -4, env);
            Prop("left_doorway.png", new Vector2(-8.3f, groundY), S, -3, env);
            Prop("left_roof_hvac.png", new Vector2(-5.2f, groundY), S, -3, env);
            Prop("left_electric_box.png", new Vector2(-3.4f, groundY), S, -3, env);
            Prop("left_cables.png", new Vector2(-6.5f, groundY + 3.2f), S, -4, env);
            Prop("coiled_cable.png", new Vector2(-2.2f, groundY), S, -2, env);
            Prop("right_water_tank.png", new Vector2(8.6f, groundY), S, -4, env);
            Prop("right_satellite_dish.png", new Vector2(6.2f, groundY), S, -3, env);
            Prop("edge_planter.png", new Vector2(4.7f, groundY), S, -2, env);

            // Floor: reuse the stage 1 rooftop wall tile
            var tile = SpriteAt("Assets/Art/Sprites/Tile/Tile_1.png");
            var floor = Empty("Floor", Vector3.zero, env);
            floor.layer = Layers.Ground;
            if (tile != null)
            {
                float w = tile.bounds.size.x, h = tile.bounds.size.y;
                float scale = 1.2f / Mathf.Max(0.01f, h) * 2f;
                for (float x = -halfW - w * scale; x < halfW + w * scale; x += w * scale)
                {
                    var t = new GameObject("FloorTile");
                    t.transform.SetParent(floor.transform, false);
                    var sr = t.AddComponent<SpriteRenderer>();
                    sr.sprite = tile; sr.sharedMaterial = Mat("Sprite-Lit-Default.mat"); sr.sortingOrder = 5;
                    t.transform.localScale = Vector3.one * scale;
                    t.transform.position = new Vector3(x + w * scale / 2f, groundY - (tile.bounds.max.y * scale), 0);
                }
            }
            var fc = floor.AddComponent<BoxCollider2D>();
            fc.size = new Vector2(halfW * 2f + 4f, 2f); fc.offset = new Vector2(0, groundY - 1f);

            Wall("Wall_Left", new Vector2(-halfW - 0.5f, 6f), new Vector2(1f, 20f));
            Wall("Wall_Right", new Vector2(halfW + 0.5f, 6f), new Vector2(1f, 20f));
            Kill(new Vector2(0, groundY - 8f), new Vector2(40f, 2f));

            var sc = MakeStageController("BOSS", "혼령의 마녀", new Vector3(-6f, groundY + 0.2f), new Rect(-halfW, groundY - 1.6f, halfW * 2f, 13f), 6f);
            sc.cameraOffset = new Vector2(0f, 3f);
            sc.requireClear = false;
            sc.bossIntroLines = new[] { "저 녀석이... 이 혼돈의 근원이에요!", "『혼령의 마녀』. 조심하세요, 창조자님!" };
            sc.clearLines = new[] { "해냈어요, 창조자님!", "달빛이... 조금 돌아온 것 같아요." };

            var bossPrefab = Load<GameObject>($"{PrefabRoot}/Boss_SoulWitch.prefab");
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(bossPrefab, scene);
            boss.transform.position = new Vector3(6f, groundY + 3.6f, 0);
            var bw = boss.GetComponent<BossWitch>();
            bw.arenaMinX = -halfW; bw.arenaMaxX = halfW; bw.groundY = groundY; bw.hoverHeight = 3.6f;
            sc.boss = bw;
            GuideSpiritObject(new Vector3(-7f, groundY + 2.4f));
            cam.transform.position = new Vector3(0, groundY + 4.4f, -10f);
            Save(scene, "BossStage");
            return "Scene BossStage";
        }
    }

    /// <summary>Tiny JSON reader (objects, arrays, numbers, strings, bools) for the atlas files.</summary>
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; return Value(s, ref i); }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(); i++;
                Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = (string)Value(s, ref i); Ws(s, ref i); i++; // ':'
                    d[k] = Value(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d;
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++;
                Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l;
                }
            }
            if (c == '"')
            {
                var sb = new System.Text.StringBuilder(); i++;
                while (s[i] != '"')
                {
                    if (s[i] == '\\') { i++; char e = s[i]; sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'u' ? (char)System.Convert.ToInt32(s.Substring(i + 1, 4), 16) : e); if (e == 'u') i += 4; }
                    else sb.Append(s[i]);
                    i++;
                }
                i++; return sb.ToString();
            }
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(st, i - st), System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
#endif
