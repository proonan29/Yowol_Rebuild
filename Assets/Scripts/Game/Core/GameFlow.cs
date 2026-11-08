using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yowol
{
    /// <summary>Scene order and transitions: Title > Intro > Stage1 > Stage2 > BossStage.</summary>
    public static class GameFlow
    {
        public const string Title = "Title";
        public const string Intro = "Intro";
        public const string Stage1 = "Stage1";
        public const string Stage2 = "Stage2";
        public const string BossStage = "BossStage";
        public static readonly string[] Order = { Title, Intro, Stage1, Stage2, BossStage };

        const string ContinueKey = "yowol.continueScene";

        public static string CurrentScene => SceneManager.GetActiveScene().name;

        public static void NewGame()
        {
            RunState.Reset();
            Load(Intro);
        }

        public static void LoadNext()
        {
            int i = System.Array.IndexOf(Order, CurrentScene);
            string next = (i >= 0 && i < Order.Length - 1) ? Order[i + 1] : Title;
            Load(next);
        }

        /// <summary>Load by name. "Continue" resumes the last stage reached.</summary>
        public static void Load(string scene)
        {
            if (scene == "Continue") scene = PlayerPrefs.GetString(ContinueKey, Stage1);
            if (scene == "MainStageSecen") scene = Intro;          // legacy name used by old buttons
            if (scene == Stage1 || scene == Stage2 || scene == BossStage)
            {
                PlayerPrefs.SetString(ContinueKey, scene);
                PlayerPrefs.Save();
            }
            Time.timeScale = 1f;
            GameInput.Locked = false;
            ScreenFader.Instance.FadeToScene(scene);
        }

        public static void RestartStage() => Load(CurrentScene);
        public static bool HasContinue => PlayerPrefs.HasKey(ContinueKey);
    }

    /// <summary>State carried between stages.</summary>
    public static class RunState
    {
        public static int WeaponIndex;
        public static void Reset() { WeaponIndex = 0; }
    }

    /// <summary>Physics layers (indices match ProjectSettings/TagManager).</summary>
    public static class Layers
    {
        public const int Default = 0, Player = 6, Ground = 7, Enemy = 8, PlayerAttack = 11, EnemyAttack = 12;
        public const int GroundMask = 1 << Ground;
        public const int EnemyMask = 1 << Enemy;
        public const int PlayerMask = 1 << Player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SetupCollisionMatrix()
        {
            Physics2D.IgnoreLayerCollision(Player, Enemy, true);
            Physics2D.IgnoreLayerCollision(Enemy, Enemy, true);
            for (int i = 0; i < 32; i++)
            {
                Physics2D.IgnoreLayerCollision(PlayerAttack, i, true);
                Physics2D.IgnoreLayerCollision(EnemyAttack, i, true);
            }
        }
    }
}
