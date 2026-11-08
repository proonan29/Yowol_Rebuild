using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class SoundOptionManager : MonoBehaviour
{
    [Header("Audio Mixer (선택 사항)")]
    [SerializeField] private AudioMixer mainAudioMixer; // AudioMixer 사용 시 연결

    [Header("Volume Sliders")]
    [SerializeField] private Slider masterVolumeSlider; // 전체 음량
    [SerializeField] private Slider bgmVolumeSlider;    // 음악 음량
    [SerializeField] private Slider sfxVolumeSlider;    // 효과음 음량
    [SerializeField] private Slider systemVolumeSlider; // 시스템 음량

    private const string MASTER_KEY = "MasterVolume";
    private const string BGM_KEY = "BGMVolume";
    private const string SFX_KEY = "SFXVolume";
    private const string SYSTEM_KEY = "SystemVolume";

    private void Awake()
    {
        // 1. 슬라이더 범위 초기화 (0.0001 ~ 1.0)
        InitSlider(masterVolumeSlider, OnMasterVolumeChanged);
        InitSlider(bgmVolumeSlider, OnBGMVolumeChanged);
        InitSlider(sfxVolumeSlider, OnSFXVolumeChanged);
        InitSlider(systemVolumeSlider, OnSystemVolumeChanged);
    }

    private void Start()
    {
        // 2. 저장된 볼륨 값 불러오기 (기본값 0.8f = 80%)
        float master = PlayerPrefs.GetFloat(MASTER_KEY, 0.8f);
        float bgm = PlayerPrefs.GetFloat(BGM_KEY, 0.8f);
        float sfx = PlayerPrefs.GetFloat(SFX_KEY, 0.8f);
        float system = PlayerPrefs.GetFloat(SYSTEM_KEY, 0.8f);

        // UI 슬라이더 값 적용
        if (masterVolumeSlider != null) masterVolumeSlider.value = master;
        if (bgmVolumeSlider != null) bgmVolumeSlider.value = bgm;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfx;
        if (systemVolumeSlider != null) systemVolumeSlider.value = system;

        // 실제 볼륨 적용
        ApplyVolume(MASTER_KEY, master);
        ApplyVolume(BGM_KEY, bgm);
        ApplyVolume(SFX_KEY, sfx);
        ApplyVolume(SYSTEM_KEY, system);
    }

    private void InitSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider != null)
        {
            slider.minValue = 0.0001f; // Log10(0) 방지용 최소값
            slider.maxValue = 1f;
            slider.wholeNumbers = false; // 볼륨은 부드럽게 조절되도록 false
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(action);
        }
    }

    // --- 슬라이더 변경 이벤트들 ---
    private void OnMasterVolumeChanged(float value)
    {
        ApplyVolume(MASTER_KEY, value);
        PlayerPrefs.SetFloat(MASTER_KEY, value);
        AudioListener.volume = value; // 전체 볼륨 제어
    }

    private void OnBGMVolumeChanged(float value)
    {
        ApplyVolume(BGM_KEY, value);
        PlayerPrefs.SetFloat(BGM_KEY, value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        ApplyVolume(SFX_KEY, value);
        PlayerPrefs.SetFloat(SFX_KEY, value);
    }

    private void OnSystemVolumeChanged(float value)
    {
        ApplyVolume(SYSTEM_KEY, value);
        PlayerPrefs.SetFloat(SYSTEM_KEY, value);
    }

    // --- AudioMixer 또는 음량 적용 함수 ---
    private void ApplyVolume(string parameterName, float linearValue)
    {
        if (mainAudioMixer != null)
        {
            // Linear(0~1) -> Logarithmic dB 변환 (-80dB ~ 0dB)
            float dB = Mathf.Log10(linearValue) * 20f;
            mainAudioMixer.SetFloat(parameterName, dB);
        }
    }
}