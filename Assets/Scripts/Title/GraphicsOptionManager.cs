using UnityEngine;
using UnityEngine.UI;

public class GraphicsOptionManager : MonoBehaviour
{
    [Header("Main Preset Slider (5 Steps)")]
    [SerializeField] private Slider mainQualitySlider; // 최저~최고 사양 슬라이더 (Whole Numbers 설정)

    [Header("Auto Settings Button")]
    [SerializeField] private Button autoSettingButton; // 자동 설정 버튼

    [Header("Detail Settings Toggle")]
    [SerializeField] private Button detailToggleButton; // 세부 설정 옆 아이콘/버튼
    [SerializeField] private GameObject detailSettingsGroup; // 세부 설정 슬라이더들이 모인 그룹 Panel

    [Header("Detail Sliders (0 ~ 3)")]
    [SerializeField] private Slider textureSlider;   // 텍스처 해상도
    [SerializeField] private Slider viewDistanceSlider; // 사물 시야 거리
    [SerializeField] private Slider waterSlider;      // 물 처리
    [SerializeField] private Slider shadowSlider;     // 그림자 품질

    private bool isUpdatingUI = false;

    private void Awake()
    {
        // 1. 메인 품질 슬라이더 설정 (0: 최저, 1: 저사양, 2: 보통, 3: 고사양, 4: 최고사양)
        if (mainQualitySlider != null)
        {
            mainQualitySlider.minValue = 0;
            mainQualitySlider.maxValue = 4;
            mainQualitySlider.wholeNumbers = true; // 정수 단계로 스냅(Snap) 되도록 설정
            mainQualitySlider.onValueChanged.AddListener(OnMainQualityChanged);
        }

        // 2. 세부 설정 슬라이더 이벤트 연동
        if (textureSlider != null) textureSlider.onValueChanged.AddListener(OnDetailSettingChanged);
        if (viewDistanceSlider != null) viewDistanceSlider.onValueChanged.AddListener(OnDetailSettingChanged);
        if (waterSlider != null) waterSlider.onValueChanged.AddListener(OnDetailSettingChanged);
        if (shadowSlider != null) shadowSlider.onValueChanged.AddListener(OnDetailSettingChanged);

        // 3. 버튼 이벤트 연동
        if (autoSettingButton != null)
        {
            autoSettingButton.onClick.AddListener(AutoDetectGraphicsSettings);
        }

        if (detailToggleButton != null)
        {
            detailToggleButton.onClick.AddListener(ToggleDetailSettings);
        }
    }

    private void Start()
    {
        // 현재 유니티 프로젝트 퀄리티 레벨에 맞추어 초기 슬라이더 상태 업데이트
        int currentQuality = QualitySettings.GetQualityLevel();
        int clampedLevel = Mathf.Clamp(currentQuality, 0, 4);

        if (mainQualitySlider != null)
        {
            mainQualitySlider.value = clampedLevel;
        }

        // 세부 설정 패널은 기본적으로 숨김
        if (detailSettingsGroup != null)
        {
            detailSettingsGroup.SetActive(false);
        }
    }

    // --- 1. 메인 품질 슬라이더 변경 시 호출 ---
    private void OnMainQualityChanged(float value)
    {
        if (isUpdatingUI) return;

        int level = Mathf.RoundToInt(value);

        // 유니티 프로젝트의 Quality Level 변경
        QualitySettings.SetQualityLevel(level, true);

        // 세부 설정 슬라이더들도 메인 프리셋 단계에 맞춰 일괄 조정
        isUpdatingUI = true;
        SetDetailSlidersValue(level);
        isUpdatingUI = false;

        PlaySound();
    }

    // --- 2. 세부 설정 슬라이더 변경 시 호출 ---
    private void OnDetailSettingChanged(float value)
    {
        if (isUpdatingUI) return;

        // 세부 슬라이더 변경에 맞춰 개별 그래픽 옵션 적용
        ApplyDetailSettings();
        PlaySound();
    }

    private void SetDetailSlidersValue(int level)
    {
        // level (0 ~ 4)을 세부 슬라이더 범위에 맞게 조정 (예: 0~3 단계)
        float detailValue = Mathf.Clamp(level, 0, 3);

        if (textureSlider != null) textureSlider.value = detailValue;
        if (viewDistanceSlider != null) viewDistanceSlider.value = detailValue;
        if (waterSlider != null) waterSlider.value = detailValue;
        if (shadowSlider != null) shadowSlider.value = detailValue;
    }

    private void ApplyDetailSettings()
    {
        // A. 텍스처 해상도 설정 (0: Full, 1: Half, 2: Quarter 등)
        if (textureSlider != null)
        {
            int texVal = 3 - Mathf.RoundToInt(textureSlider.value); // 높은 값일수록 고화질
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(texVal, 0, 2);
        }

        // B. 그림자 품질 설정
        if (shadowSlider != null)
        {
            int shadowVal = Mathf.RoundToInt(shadowSlider.value);
            if (shadowVal == 0) QualitySettings.shadows = ShadowQuality.Disable;
            else if (shadowVal == 1) QualitySettings.shadows = ShadowQuality.HardOnly;
            else QualitySettings.shadows = ShadowQuality.All;
        }

        // C. 시야거리 / 물 처리는 게임 내 라이팅/카메라 스크립트에 따라 가공하여 사용
    }

    // --- 3. 하드웨어 사양 기반 자동 설정 ---
    public void AutoDetectGraphicsSettings()
    {
        int vramMB = SystemInfo.graphicsMemorySize;      // VRAM 용량 (MB)
        int ramMB = SystemInfo.systemMemorySize;          // RAM 용량 (MB)
        int cpuCores = SystemInfo.processorCount;         // CPU 코어 수

        int recommendedLevel = 2; // 기본 보통 사양

        // 간단한 PC 사양 감지 로직
        if (vramMB >= 8000 && ramMB >= 16000 && cpuCores >= 8)
        {
            recommendedLevel = 4; // 최고 사양
        }
        else if (vramMB >= 6000 && ramMB >= 16000)
        {
            recommendedLevel = 3; // 고사양
        }
        else if (vramMB >= 4000 && ramMB >= 8000)
        {
            recommendedLevel = 2; // 보통
        }
        else if (vramMB >= 2000)
        {
            recommendedLevel = 1; // 저사양
        }
        else
        {
            recommendedLevel = 0; // 최저 사양
        }

        // 슬라이더 변경을 통해 옵션 반영
        if (mainQualitySlider != null)
        {
            mainQualitySlider.value = recommendedLevel;
        }

        PlaySound();
    }

    // --- 4. 세부 설정 토글 ---
    public void ToggleDetailSettings()
    {
        if (detailSettingsGroup != null)
        {
            bool isActive = !detailSettingsGroup.activeSelf;
            detailSettingsGroup.SetActive(isActive);
        }

        PlaySound();
    }

    private void PlaySound()
    {
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClickSound();
        }
    }
}