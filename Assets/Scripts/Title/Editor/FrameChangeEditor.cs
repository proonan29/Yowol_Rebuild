using UnityEngine;
using UnityEditor;
using UnityEditor.UI; // Button 에디터를 쓰기 위해 필수

// 1. 내가 만든 파생 클래스(MenuButton)의 인스펙터를 커스텀하겠다고 선언
[CustomEditor(typeof(FrameChange))]
// 2. 중요: 원래 유니티 버튼 에디터인 'ButtonEditor'를 상속받습니다.
public class FrameChangeEditor : ButtonEditor
{
    public override void OnInspectorGUI()
    {
        // 원래 유니티 버튼의 인스펙터(On Click 등)를 먼저 그리게 합니다.
        base.OnInspectorGUI();

        // 한 줄 띄우고 구분선 생성 (시각적 정돈)
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Custom Subclass Properties", EditorStyles.boldLabel);

        // 중요: MenuButton에 작성된 public/Serialized 변수들을 자동으로 찾아 인스펙터에 그려줍니다.
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
    }
}