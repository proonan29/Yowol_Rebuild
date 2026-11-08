using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance {  get; private set; }

    [Header("Audio Components")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip buttonClickClip;

   private void Awake()
   {
        // 씬이 변경되어도 파괴되지 않는 싱글톤 구성
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
   }

    // 어디서든 SoundManager.Instance.PlayButtonClickSound(); 로 호출 가능
    public void PlayButtonClickSound()
    {
        if (sfxSource != null && buttonClickClip != null)
        {
            sfxSource.PlayOneShot(buttonClickClip);
        }
    }
}
