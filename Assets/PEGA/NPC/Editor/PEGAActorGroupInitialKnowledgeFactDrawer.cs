using System;
using System.Linq;
using System.Reflection;
using PEGA.NPC;
using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    [CustomPropertyDrawer(typeof(PEGAActorGroupInitialKnowledgeFact), true)]
    public sealed class PEGAActorGroupInitialKnowledgeFactDrawer : PropertyDrawer
    {
        private const float TypeButtonWidth = 54f;
        private static Type[] _entryTypes;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            if (property.managedReferenceValue == null || !property.isExpanded)
            {
                return lineHeight;
            }

            float height = lineHeight + EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty child = property.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                enterChildren = false;
                if (child.depth <= property.depth)
                {
                    break;
                }

                height += EditorGUI.GetPropertyHeight(child, true) + EditorGUIUtility.standardVerticalSpacing;
            }

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            if (property.managedReferenceValue == null)
            {
                using (new EditorGUI.DisabledScope(property.serializedObject.isEditingMultipleObjects))
                {
                    if (GUI.Button(position, "Selecionar fato inicial suportado"))
                    {
                        ShowEntryMenu(property);
                    }
                }

                EditorGUI.EndProperty();
                return;
            }

            Type currentType = property.managedReferenceValue.GetType();
            PEGAActorGroupInitialKnowledgeFactMenuAttribute menuAttribute =
                currentType.GetCustomAttribute<PEGAActorGroupInitialKnowledgeFactMenuAttribute>();
            string title = menuAttribute != null ? menuAttribute.MenuName : currentType.Name;
            Rect header = new Rect(position.x, position.y, position.width - TypeButtonWidth, EditorGUIUtility.singleLineHeight);
            Rect typeButton = new Rect(
                position.xMax - TypeButtonWidth,
                position.y,
                TypeButtonWidth,
                EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, title, true);
            using (new EditorGUI.DisabledScope(property.serializedObject.isEditingMultipleObjects))
            {
                if (GUI.Button(typeButton, "Tipo"))
                {
                    ShowEntryMenu(property);
                }
            }

            if (property.isExpanded)
            {
                int previousIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel++;
                float y = position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                SerializedProperty child = property.Copy();
                SerializedProperty end = child.GetEndProperty();
                bool enterChildren = true;
                while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
                {
                    enterChildren = false;
                    if (child.depth <= property.depth)
                    {
                        break;
                    }

                    float childHeight = EditorGUI.GetPropertyHeight(child, true);
                    Rect childRect = new Rect(position.x, y, position.width, childHeight);
                    EditorGUI.PropertyField(childRect, child, true);
                    y += childHeight + EditorGUIUtility.standardVerticalSpacing;
                }

                EditorGUI.indentLevel = previousIndent;
            }

            EditorGUI.EndProperty();
        }

        private static void ShowEntryMenu(SerializedProperty property)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            Type[] entryTypes = GetEntryTypes();
            if (entryTypes.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("Nenhum fato inicial suportado"));
            }
            else
            {
                Type currentType = property.managedReferenceValue?.GetType();
                for (int index = 0; index < entryTypes.Length; index++)
                {
                    Type entryType = entryTypes[index];
                    PEGAActorGroupInitialKnowledgeFactMenuAttribute menuAttribute =
                        entryType.GetCustomAttribute<PEGAActorGroupInitialKnowledgeFactMenuAttribute>();
                    string menuName = menuAttribute != null ? menuAttribute.MenuName : entryType.Name;
                    menu.AddItem(
                        new GUIContent(menuName),
                        entryType == currentType,
                        () => AssignEntry(property.serializedObject.targetObject, property.propertyPath, entryType));
                }
            }

            menu.ShowAsContext();
        }

        private static void AssignEntry(UnityEngine.Object host, string propertyPath, Type entryType)
        {
            if (host == null || entryType == null || entryType.IsAbstract || entryType.ContainsGenericParameters)
            {
                return;
            }

            Undo.RecordObject(host, "Choose Actor Group initial fact");
            var serializedObject = new SerializedObject(host);
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return;
            }

            property.managedReferenceValue = Activator.CreateInstance(entryType);
            serializedObject.ApplyModifiedProperties();
        }

        private static Type[] GetEntryTypes()
        {
            if (_entryTypes == null)
            {
                _entryTypes = TypeCache.GetTypesDerivedFrom<PEGAActorGroupInitialKnowledgeFact>()
                    .Where(type => !type.IsAbstract && !type.ContainsGenericParameters &&
                        type.GetCustomAttribute<PEGAActorGroupInitialKnowledgeFactMenuAttribute>() != null)
                    .OrderBy(type => type.GetCustomAttribute<PEGAActorGroupInitialKnowledgeFactMenuAttribute>().MenuName,
                        StringComparer.Ordinal)
                    .ToArray();
            }

            return _entryTypes;
        }
    }
}
