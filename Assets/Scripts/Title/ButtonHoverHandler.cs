using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonHoverHandler : MonoBehaviour, IPointerEnterHandler
{
    private SelectionManager manager;
    private int buttonIndex;

    public void Setup(SelectionManager selectionManager, int index)
    {
        manager = selectionManager;
        buttonIndex = index;
    }

    // 마우스가 UI 영역 위로 올라갔을 때 이벤트 발생
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.SetSelection(buttonIndex);
        }
    }
}