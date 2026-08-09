using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FloatValue))]
public class FloatValueDrawer : PropertyDrawer
{
    private const float IndentWidth = 15f;
    private const float Spacing = 2f;

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Draw the foldout arrow and label
        Rect foldoutRect = new Rect(
            position.x,
            position.y,
            EditorGUIUtility.labelWidth,
            EditorGUIUtility.singleLineHeight
        );

        property.isExpanded = EditorGUI.Foldout(
            foldoutRect,
            property.isExpanded,
            label,
            true
        );

        // Draw the ScriptableObject reference
        Rect objectRect = new Rect(
            position.x + EditorGUIUtility.labelWidth,
            position.y,
            position.width - EditorGUIUtility.labelWidth,
            EditorGUIUtility.singleLineHeight
        );

        EditorGUI.PropertyField(
            objectRect,
            property,
            GUIContent.none
        );

        // Draw the value if expanded and a FloatValue is assigned
        if (property.isExpanded && property.objectReferenceValue != null)
        {
            SerializedObject floatValueObject =
                new SerializedObject(property.objectReferenceValue);

            floatValueObject.Update();

            SerializedProperty value =
                floatValueObject.FindProperty("value");

            float childY =
                position.y +
                EditorGUIUtility.singleLineHeight +
                Spacing;

            // Indented label for the child value
            Rect valueLabelRect = new Rect(
                position.x + IndentWidth,
                childY,
                EditorGUIUtility.labelWidth - IndentWidth,
                EditorGUIUtility.singleLineHeight
            );

            EditorGUI.LabelField(
                valueLabelRect,
                "Value"
            );

            // Keep the input field aligned with the reference field
            Rect valueFieldRect = new Rect(
                position.x + EditorGUIUtility.labelWidth,
                childY,
                position.width - EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight
            );

            EditorGUI.PropertyField(
                valueFieldRect,
                value,
                GUIContent.none
            );

            floatValueObject.ApplyModifiedProperties();
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;

        if (property.isExpanded && property.objectReferenceValue != null)
        {
            height += EditorGUIUtility.singleLineHeight + Spacing;
        }

        return height;
    }
}