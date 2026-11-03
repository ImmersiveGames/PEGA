using System;
using System.Collections.Generic;
using System.Linq;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    [CustomPropertyDrawer(typeof(PEGAKnowledgeFactAuthoringEntry))]
    internal sealed class PEGAKnowledgeFactAuthoringEntryPropertyDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;
        private const float HelpBoxHeight = 38f;

        private static readonly GUIContent[] EmptyOptions =
        {
            new GUIContent("<selecione uma propriedade>")
        };

        private static readonly string[] ActorIdentitySources =
        {
            "ActorDeclaration · ActorId",
            "Player Actor atual do binding · ActorId"
        };

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!TryGetSelectedDefinition(property, out IPEGAKnowledgeFactDefinition definition, out string diagnostic))
            {
                return height + Spacing + HelpBoxHeight;
            }

            if (!definition.AllowsDesignerAuthoredInitialFact ||
                !(definition is IPEGAKnowledgeFactInitialAuthoringDefinition) ||
                !PEGAKnowledgeFactAuthoringPayload.Supports(definition.AddressType) ||
                !PEGAKnowledgeFactAuthoringPayload.Supports(definition.ValueType))
            {
                return height + Spacing + HelpBoxHeight;
            }

            height += Spacing + GetPayloadHeight(
                property.FindPropertyRelative("address"),
                definition.AddressType);
            height += Spacing + GetPayloadHeight(
                property.FindPropertyRelative("value"),
                definition.ValueType);
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int originalIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            var row = new Rect(position.x, position.y, position.width, lineHeight);
            SerializedProperty definitionId = property.FindPropertyRelative("definitionId");
            List<IPEGAKnowledgeFactDefinition> definitions = GetAvailableDefinitions();
            string[] names = new string[definitions.Count + 1];
            names[0] = EmptyOptions[0].text;
            for (int index = 0; index < definitions.Count; index++)
            {
                names[index + 1] = definitions[index].Name;
            }

            int selectedIndex = definitions.FindIndex(definition => definition.Id == definitionId.stringValue) + 1;
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUI.Popup(row, "Propriedade", selectedIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                definitionId.stringValue = nextIndex > 0 && nextIndex <= definitions.Count
                    ? definitions[nextIndex - 1].Id
                    : string.Empty;
            }

            row.y += lineHeight + Spacing;
            if (!TryGetSelectedDefinition(property, out IPEGAKnowledgeFactDefinition selectedDefinition, out string issue))
            {
                EditorGUI.HelpBox(row, issue, MessageType.Info);
                EditorGUI.indentLevel = originalIndent;
                EditorGUI.EndProperty();
                return;
            }

            if (!selectedDefinition.AllowsDesignerAuthoredInitialFact ||
                !(selectedDefinition is IPEGAKnowledgeFactInitialAuthoringDefinition) ||
                !PEGAKnowledgeFactAuthoringPayload.Supports(selectedDefinition.AddressType) ||
                !PEGAKnowledgeFactAuthoringPayload.Supports(selectedDefinition.ValueType))
            {
                EditorGUI.HelpBox(row, "Esta definição não possui um payload Inspector compatível para autoria inicial.", MessageType.Warning);
                EditorGUI.indentLevel = originalIndent;
                EditorGUI.EndProperty();
                return;
            }

            SerializedProperty address = property.FindPropertyRelative("address");
            float addressHeight = GetPayloadHeight(address, selectedDefinition.AddressType);
            DrawPayload(
                new Rect(row.x, row.y, row.width, addressHeight),
                address,
                selectedDefinition.AddressType,
                new GUIContent($"Identidade · {selectedDefinition.AddressType.Name}"),
                property.serializedObject.targetObject as Component);

            row.y += addressHeight + Spacing;
            SerializedProperty value = property.FindPropertyRelative("value");
            float valueHeight = GetPayloadHeight(value, selectedDefinition.ValueType);
            DrawPayload(
                new Rect(row.x, row.y, row.width, valueHeight),
                value,
                selectedDefinition.ValueType,
                new GUIContent($"Valor · {selectedDefinition.ValueType.Name}"),
                property.serializedObject.targetObject as Component);

            EditorGUI.indentLevel = originalIndent;
            EditorGUI.EndProperty();
        }

        private static float GetPayloadHeight(SerializedProperty payload, Type type)
        {
            if (type == typeof(ActorId))
            {
                SerializedProperty source = payload.FindPropertyRelative("actorIdentitySource");
                float height = EditorGUI.GetPropertyHeight(source, true);
                if (source.enumValueIndex == (int)PEGAKnowledgeActorIdentitySource.ActorDeclaration)
                {
                    height += Spacing + EditorGUIUtility.singleLineHeight;
                }
                else
                {
                    height += Spacing + HelpBoxHeight;
                }

                return height;
            }

            SerializedProperty typedValue = FindTypedValue(payload, type);
            return typedValue == null
                ? HelpBoxHeight
                : EditorGUI.GetPropertyHeight(typedValue, true);
        }

        private static void DrawPayload(
            Rect position,
            SerializedProperty payload,
            Type type,
            GUIContent label,
            Component owner)
        {
            if (type == typeof(ActorId))
            {
                SerializedProperty source = payload.FindPropertyRelative("actorIdentitySource");
                float lineHeight = EditorGUI.GetPropertyHeight(source, true);
                var sourceRect = new Rect(position.x, position.y, position.width, lineHeight);
                EditorGUI.BeginChangeCheck();
                int selectedSource = EditorGUI.Popup(
                    sourceRect,
                    "Fonte de identidade",
                    Mathf.Clamp(source.enumValueIndex, 0, ActorIdentitySources.Length - 1),
                    ActorIdentitySources);
                if (EditorGUI.EndChangeCheck())
                {
                    source.enumValueIndex = selectedSource;
                }

                Rect detailRect = new Rect(
                    position.x,
                    sourceRect.yMax + Spacing,
                    position.width,
                    position.height - lineHeight - Spacing);
                if (source.enumValueIndex == (int)PEGAKnowledgeActorIdentitySource.ActorDeclaration)
                {
                    SerializedProperty declaration = payload.FindPropertyRelative("actorDeclaration");
                    EditorGUI.PropertyField(detailRect, declaration, label, true);
                }
                else if (source.enumValueIndex == (int)PEGAKnowledgeActorIdentitySource.CurrentPlayerActorFromNpcBinding)
                {
                    bool hasBinding = owner != null && owner.GetComponent<ActorOccurrenceReferenceBinding>() != null;
                    EditorGUI.HelpBox(
                        detailRect,
                        hasBinding
                        ? "Usa o ActorOccurrenceReferenceBinding deste owner para resolver o ActorId lógico do Player. A chave gravada continua sendo ActorId."
                        : "Adicione ActorOccurrenceReferenceBinding a este owner para resolver o ActorId lógico do Player.",
                        hasBinding ? MessageType.Info : MessageType.Warning);
                }
                else
                {
                    EditorGUI.HelpBox(detailRect, "Selecione uma fonte de identidade Actor suportada.", MessageType.Warning);
                }

                return;
            }

            SerializedProperty typedValue = FindTypedValue(payload, type);
            if (typedValue == null)
            {
                EditorGUI.HelpBox(position, $"Não há campo Inspector para {type.Name}.", MessageType.Error);
                return;
            }

            if (type.IsEnum)
            {
                DrawEnum(position, typedValue, type, label);
                return;
            }

            EditorGUI.PropertyField(position, typedValue, label, true);
        }

        private static void DrawEnum(Rect position, SerializedProperty property, Type enumType, GUIContent label)
        {
            Enum current = (Enum)Enum.ToObject(enumType, property.intValue);
            EditorGUI.BeginChangeCheck();
            Enum selected = EditorGUI.EnumPopup(position, label, current);
            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = Convert.ToInt32(selected);
            }
        }

        private static SerializedProperty FindTypedValue(SerializedProperty payload, Type type)
        {
            string fieldName = type == typeof(ObjectEntryId) ? "objectEntryId" :
                type == typeof(string) ? "stringValue" :
                type == typeof(byte) || type == typeof(int) || type.IsEnum ? "integerValue" :
                type == typeof(long) ? "longValue" :
                type == typeof(float) ? "floatValue" :
                type == typeof(double) ? "doubleValue" :
                type == typeof(bool) ? "boolValue" :
                type == typeof(Vector3) ? "vector3Value" :
                type == typeof(PEGANpcPerceivedAreaPresence) ? "perceivedAreaPresenceValue" :
                null;
            return fieldName != null ? payload.FindPropertyRelative(fieldName) : null;
        }

        private static bool TryGetSelectedDefinition(
            SerializedProperty property,
            out IPEGAKnowledgeFactDefinition definition,
            out string diagnostic)
        {
            SerializedProperty definitionId = property.FindPropertyRelative("definitionId");
            if (!string.IsNullOrWhiteSpace(definitionId.stringValue) &&
                PEGAKnowledgeFactDefinitionCatalog.TryGet(definitionId.stringValue, out definition))
            {
                diagnostic = string.Empty;
                return true;
            }

            definition = null;
            diagnostic = string.IsNullOrWhiteSpace(definitionId.stringValue)
                ? "Selecione uma propriedade canônica disponível para autoria inicial."
                : $"A propriedade '{definitionId.stringValue}' não está no catálogo atual.";
            return false;
        }

        private static List<IPEGAKnowledgeFactDefinition> GetAvailableDefinitions()
        {
            return PEGAKnowledgeFactDefinitionCatalog.All
                .Where(definition =>
                    definition.AllowsDesignerAuthoredInitialFact &&
                    definition is IPEGAKnowledgeFactInitialAuthoringDefinition &&
                    PEGAKnowledgeFactAuthoringPayload.Supports(definition.AddressType) &&
                    PEGAKnowledgeFactAuthoringPayload.Supports(definition.ValueType))
                .OrderBy(definition => definition.Name, StringComparer.Ordinal)
                .ThenBy(definition => definition.Id, StringComparer.Ordinal)
                .ToList();
        }
    }
}
