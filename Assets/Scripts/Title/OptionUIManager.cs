using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class OptionUIManager : MonoBehaviour
{
    [System.Serializable]
    public struct OptionTab
    {
        public string tabName;
        public TextMeshProUGUI tabText;
        public GameObject contentGroup;
    }

    [Header("UI Panel Root")]
    [SerializeField] private GameObject optionPanel;

    [Header("Tab Settings")]
    [SerializeField] private OptionTab[] tabs;

    [Header("Tab Colors")]
    [SerializeField] private Color activeColor = new Color(0.2f, 0.8f, 1.0f, 1.0f);
    [SerializeField] private Color inactiveColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);

    [Header("Close Event (ESC / Back Button)")]
    [Tooltip("옵션창이 닫힐 때 실행할 이벤트 (TitleUIManager.CloseOption 연결용)")]
    public UnityEvent onCloseOptions;

    private int currentTabIndex = 0;

    void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].tabText != null)
            {
                Button btn = tabs[i].tabText.GetComponent<Button>();
                if (btn == null)
                {
                    btn = tabs[i].tabText.gameObject.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                }

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    SelectTab(index, playSound: true);
                });
            }
        }
    }

    private void OnEnable()
    {
        SelectTab(0, playSound: false);
    }

    void Update()
    {
        // ESC 키 입력 시 닫기 이벤트 실행
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseOptionUI();
        }

        // Q/E 및 방향키 탭 이동
        if (optionPanel != null && optionPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.LeftArrow))
            {
                int prevTab = (currentTabIndex - 1 + tabs.Length) % tabs.Length;
                SelectTab(prevTab, playSound: true);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E))
            {
                int nextTab = (currentTabIndex + 1) % tabs.Length;
                SelectTab(nextTab, playSound: true);
            }
        }
    }

    public void CloseOptionUI()
    {
        // 1. 등록된 닫기 이벤트(TitleUIManager.CloseOption)가 있다면 먼저 실행
        if (onCloseOptions != null && onCloseOptions.GetPersistentEventCount() > 0)
        {
            onCloseOptions.Invoke();
        }
        else
        {
            // 이벤트 연결이 없으면 자체적으로만 비활성화
            if (optionPanel != null) optionPanel.SetActive(false);
            else gameObject.SetActive(false);
        }

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClickSound();
        }
    }

    public void SelectTab(int index, bool playSound = false)
    {
        if (tabs == null || tabs.Length == 0) return;
        if (index < 0 || index >= tabs.Length) return;

        if (playSound && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayButtonClickSound();
        }

        currentTabIndex = index;

        for (int i = 0; i < tabs.Length; i++)
        {
            bool isSelected = (i == index);

            if (tabs[i].contentGroup != null)
            {
                if (tabs[i].contentGroup.transform.IsChildOf(transform))
                {
                    tabs[i].contentGroup.SetActive(isSelected);
                }
            }

            if (tabs[i].tabText != null)
            {
                tabs[i].tabText.color = isSelected ? activeColor : inactiveColor;
            }
        }
    }
}