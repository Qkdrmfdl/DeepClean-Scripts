using UnityEditor;
using UnityEngine;

// TutorialSequencer.phases 리스트를 인스펙터에서 깔끔하게 보여주기 위한 드로어.
// 선택한 kind에 해당하는 필드만 표시한다.
[CustomPropertyDrawer(typeof(TutorialSequencer.TutorialPhase))]
public class TutorialPhaseDrawer : PropertyDrawer
{
    const float Pad = 2f;

    static string[] FieldsFor(TutorialSequencer.TutorialPhaseKind kind)
    {
        switch (kind)
        {
            case TutorialSequencer.TutorialPhaseKind.PlayNarrations:
                return new[] { "label", "kind", "narrationIndices", "lastNarrationAudioOnly", "respawnPlayerWhenDone", "lockToolsAtStart", "postDelay" };
            case TutorialSequencer.TutorialPhaseKind.PlayDemo:
                return new[] { "label", "kind", "demoType", "demoOverrideDelay", "demoConcurrentNarration", "respawnPlayerWhenDone", "lockToolsAtStart", "postDelay" };
            case TutorialSequencer.TutorialPhaseKind.WaitForPractice:
                return new[] { "label", "kind", "narrationsBeforePractice", "practiceStepIndex", "narrationsAfterPractice", "respawnPlayerWhenDone", "lockToolsAtStart", "postDelay" };
            case TutorialSequencer.TutorialPhaseKind.WaitForCleaningButton:
                return new[] { "label", "kind", "respawnPlayerWhenDone", "lockToolsAtStart", "postDelay" };
            default:
                return new[] { "label", "kind" };
        }
    }

    static TutorialSequencer.TutorialPhaseKind KindOf(SerializedProperty prop)
        => (TutorialSequencer.TutorialPhaseKind)prop.FindPropertyRelative("kind").enumValueIndex;

    public override void OnGUI(Rect pos, SerializedProperty prop, GUIContent label)
    {
        EditorGUI.BeginProperty(pos, label, prop);

        var kind = KindOf(prop);
        string labelText = prop.FindPropertyRelative("label").stringValue;
        string header = string.IsNullOrEmpty(labelText) ? kind.ToString() : $"{labelText}   —   {kind}";

        var line = new Rect(pos.x, pos.y, pos.width, EditorGUIUtility.singleLineHeight);
        prop.isExpanded = EditorGUI.Foldout(line, prop.isExpanded, header, true);

        if (prop.isExpanded)
        {
            EditorGUI.indentLevel++;
            foreach (var name in FieldsFor(kind))
            {
                var p = prop.FindPropertyRelative(name);
                if (p == null) continue;

                line.y += line.height + Pad;
                float h = EditorGUI.GetPropertyHeight(p, true);
                EditorGUI.PropertyField(new Rect(line.x, line.y, line.width, h), p, true);
                line.y += h - line.height;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty prop, GUIContent label)
    {
        float lineH = EditorGUIUtility.singleLineHeight;
        if (!prop.isExpanded) return lineH + Pad;

        float total = lineH + Pad; // foldout
        foreach (var name in FieldsFor(KindOf(prop)))
        {
            var p = prop.FindPropertyRelative(name);
            if (p == null) continue;
            total += EditorGUI.GetPropertyHeight(p, true) + Pad;
        }
        return total + Pad * 2f;
    }
}
