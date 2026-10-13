using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FaceControllerTester))]
public sealed class FaceControllerTesterEditor : Editor
{
    private SerializedProperty controllerProperty;

    private void OnEnable()
    {
        controllerProperty = serializedObject.FindProperty("controller");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(controllerProperty);
        serializedObject.ApplyModifiedProperties();

        FaceControllerTester tester = (FaceControllerTester)target;
        FaceController controller = tester.Controller;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Face Controller Tester", EditorStyles.boldLabel);

        if (controller == null)
        {
            EditorGUILayout.HelpBox("FaceController reference is missing.", MessageType.Info);
            return;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test states and animations.", MessageType.Info);
        }

        EditorGUI.BeginDisabledGroup(!Application.isPlaying);

        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.Update();
        DrawStates(controller, controllerObject.FindProperty("states"));
        DrawAnimations(controller, controllerObject.FindProperty("animations"));
        DrawControls(controller);

        EditorGUI.EndDisabledGroup();
    }

    private static void DrawStates(FaceController controller, SerializedProperty statesProperty)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("States", EditorStyles.boldLabel);

        if (statesProperty == null || !statesProperty.isArray)
        {
            return;
        }

        for (int i = 0; i < statesProperty.arraySize; i++)
        {
            SerializedProperty itemProperty = statesProperty.GetArrayElementAtIndex(i);
            FaceState state = itemProperty.objectReferenceValue as FaceState;

            if (state == null || string.IsNullOrWhiteSpace(state.Id))
            {
                continue;
            }

            if (GUILayout.Button(state.Id))
            {
                controller.SetState(state.Id);
            }
        }
    }

    private static void DrawAnimations(FaceController controller, SerializedProperty animationsProperty)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Animations", EditorStyles.boldLabel);

        if (animationsProperty == null || !animationsProperty.isArray)
        {
            return;
        }

        for (int i = 0; i < animationsProperty.arraySize; i++)
        {
            SerializedProperty itemProperty = animationsProperty.GetArrayElementAtIndex(i);
            FaceAnimation animation = itemProperty.objectReferenceValue as FaceAnimation;

            if (animation == null || string.IsNullOrWhiteSpace(animation.Id))
            {
                continue;
            }

            if (GUILayout.Button(animation.Id))
            {
                controller.PlayAnimation(animation.Id);
            }
        }
    }

    private static void DrawControls(FaceController controller)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Stop All Animations"))
        {
            controller.StopAllAnimations();
        }
    }
}
