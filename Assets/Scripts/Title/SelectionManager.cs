using UnityEngine;
using System.Collections.Generic;
using System;

public class SelectionManager : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform selectionObj; // 이동시킬 등불 오브젝트
    public List<RectTransform> buttonList = new List<RectTransform>(); // TMPro 버튼들의 RectTransform 리스트

    [Header("Position Offset")]
    public float offsetX = -150f; // 버튼 중심으로부터의 X축 거리

    private int mySelection = 0;

    void Start()
    {
        // 리스트의 각 버튼들에게 자식 스크립트(ButtonHoverHandler) 등록 및 인덱스 부여
        for (int i = 0; i < buttonList.Count; i++)
        {
            if (buttonList[i] == null) continue;

            ButtonHoverHandler hoverHandler = buttonList[i].GetComponent<ButtonHoverHandler>();
            if (hoverHandler == null)
            {
                hoverHandler = buttonList[i].gameObject.AddComponent<ButtonHoverHandler>();
            }

            hoverHandler.Setup(this, i);
        }

        // 시작 시 첫 번째 버튼 옆으로 등불 배치
        MoveSelection();
    }

    void Update()
    {
        // 키보드 방향키 입력 처리
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            mySelection--;
            MoveSelection();
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            mySelection++;
            MoveSelection();
        }
    }

    public void MoveSelection()
    {
        if (buttonList.Count == 0 || selectionObj == null) return;

        mySelection = Math.Clamp(mySelection, 0, buttonList.Count - 1);

        // UI Canvas 기준 anchoredPosition 계산
        Vector2 targetPosition = buttonList[mySelection].anchoredPosition;
        selectionObj.anchoredPosition = targetPosition + new Vector2(offsetX, 0f);
    }

    public void SetSelection(int index)
    {
        mySelection = index;
        MoveSelection();
    }
}