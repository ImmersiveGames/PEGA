using System;
using System.Linq;
using Immersive.Framework.Actors;
using Immersive.Framework.ObjectEntry;
using PEGA.NPC;
using UnityEditor;
using UnityEngine;

namespace PEGA.NPC.Editor
{
    [CustomPropertyDrawer(typeof(PEGAActorGroupInitialKnowledgeFact))]
    internal sealed class PEGAActorGroupInitialKnowledgeFactDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return line;
            }

            float height = line + Spacing + line + Spacing;
            if (!TryGetSelectedDefinition(property, out IPEGAKnowledgeFactDefinition definition))
            {
                return height + EditorGUIUtility.singleLineHeight * 2f + Spacing;
            }

            return height + PayloadHeight(property.FindPropertyRelative("address"), definition.AddressType) + Spacing +
                   PayloadHeight(property.FindPropertyRelative("value"), definition.ValueType);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect header = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(header, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            float y = header.yMax + Spacing;
            SerializedProperty definitionId = property.FindPropertyRelative("factDefinitionId");
            Rect selector = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
            string selectedLabel = TryGetSelectedDefinition(property, out IPEGAKnowledgeFactDefinition selected)
                ? FormatDefinition(selected)
                : string.IsNullOrWhiteSpace(definitionId.stringValue) ? "Selecionar definição de fato" :
                    $"Definição indisponível: {definitionId.stringValue}";
            if (EditorGUI.DropdownButton(selector, new GUIContent(selectedLabel), FocusType.Keyboard, EditorStyles.popup))
            {
                ShowDefinitionMenu(property);
            }

            y = selector.yMax + Spacing;
            if (!TryGetSelectedDefinition(property, out selected))
            {
                var help = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight * 2f);
                EditorGUI.HelpBox(help, GetUnavailabilityMessage(definitionId.stringValue), MessageType.Warning);
                EditorGUI.EndProperty();
                return;
            }

            SerializedProperty address = property.FindPropertyRelative("address");
            float addressHeight = DrawPayload(
                new Rect(position.x, y, position.width, PayloadHeight(address, selected.AddressType)),
                address, selected.AddressType, "Endereço");
            y += addressHeight + Spacing;
            SerializedProperty value = property.FindPropertyRelative("value");
            DrawPayload(new Rect(position.x, y, position.width, PayloadHeight(value, selected.ValueType)),
                value, selected.ValueType, "Valor");
            EditorGUI.EndProperty();
        }

        private static float DrawPayload(Rect rect, SerializedProperty payload, Type type, string label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (type == typeof(ActorId))
            {
                SerializedProperty declaration = payload.FindPropertyRelative("actorDeclaration");
                SerializedProperty idText = payload.FindPropertyRelative("actorIdText");
                Rect objectRect = new Rect(rect.x, rect.y, rect.width, line);
                EditorGUI.BeginChangeCheck();
                ActorDeclaration selected = EditorGUI.ObjectField(
                    objectRect,
                    label,
                    declaration.objectReferenceValue,
                    typeof(ActorDeclaration),
                    true) as ActorDeclaration;
                if (EditorGUI.EndChangeCheck())
                {
                    declaration.objectReferenceValue = selected;
                    idText.stringValue = TryGetActorId(selected, out ActorId id, out _) ? id.Value.Value : string.Empty;
                }

                DrawIdentityStatus(new Rect(rect.x, objectRect.yMax + Spacing, rect.width, line),
                    idText.stringValue, "ActorId");
                return line * 2f + Spacing;
            }

            if (type == typeof(ActorProfileId))
            {
                SerializedProperty profile = payload.FindPropertyRelative("actorProfile");
                SerializedProperty idText = payload.FindPropertyRelative("actorProfileIdText");
                Rect objectRect = new Rect(rect.x, rect.y, rect.width, line);
                EditorGUI.BeginChangeCheck();
                ActorProfile selected = EditorGUI.ObjectField(
                    objectRect,
                    label,
                    profile.objectReferenceValue,
                    typeof(ActorProfile),
                    false) as ActorProfile;
                if (EditorGUI.EndChangeCheck())
                {
                    profile.objectReferenceValue = selected;
                    idText.stringValue = selected != null &&
                        selected.TryGetActorProfileId(out ActorProfileId id, out _) ? id.Value.Value : string.Empty;
                }

                DrawIdentityStatus(new Rect(rect.x, objectRect.yMax + Spacing, rect.width, line),
                    idText.stringValue, "ActorProfileId");
                return line * 2f + Spacing;
            }

            if (type == typeof(ObjectEntryId))
            {
                SerializedProperty identity = payload.FindPropertyRelative("objectEntryId");
                float height = EditorGUI.GetPropertyHeight(identity, new GUIContent(label), true);
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, height), identity, new GUIContent(label), true);
                return height;
            }

            string fieldName = GetPayloadFieldName(type);
            SerializedProperty field = payload.FindPropertyRelative(fieldName);
            if (type == typeof(byte))
            {
                field.intValue = EditorGUI.IntField(
                    new Rect(rect.x, rect.y, rect.width, line), label, field.intValue);
            }
            else if (type.IsEnum)
            {
                object enumValue = Enum.ToObject(type, field.intValue);
                object next = EditorGUI.EnumPopup(new Rect(rect.x, rect.y, rect.width, line), label, (Enum)enumValue);
                field.intValue = Convert.ToInt32(next);
            }
            else
            {
                EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, rect.height), field,
                    new GUIContent(label), true);
            }

            return EditorGUI.GetPropertyHeight(field, new GUIContent(label), true);
        }

        private static float PayloadHeight(SerializedProperty payload, Type type)
        {
            if (payload == null)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            if (type == typeof(ActorId) || type == typeof(ActorProfileId))
            {
                return EditorGUIUtility.singleLineHeight * 2f + Spacing;
            }

            if (type == typeof(ObjectEntryId))
            {
                return EditorGUI.GetPropertyHeight(
                    payload.FindPropertyRelative("objectEntryId"), new GUIContent(type.Name), true);
            }

            SerializedProperty field = payload.FindPropertyRelative(GetPayloadFieldName(type));
            return EditorGUI.GetPropertyHeight(field, new GUIContent(type.Name), true);
        }

        private static string GetPayloadFieldName(Type type)
        {
            if (type == typeof(string)) return "stringValue";
            if (type == typeof(byte) || type == typeof(int) || type.IsEnum) return "integerValue";
            if (type == typeof(long)) return "longValue";
            if (type == typeof(float)) return "floatValue";
            if (type == typeof(double)) return "doubleValue";
            if (type == typeof(bool)) return "boolValue";
            if (type == typeof(Vector3)) return "vector3Value";
            if (type == typeof(PEGANpcPerceivedAreaPresence)) return "perceivedAreaPresenceValue";
            throw new ArgumentException($"Unsupported fact authoring data type '{type.FullName}'.", nameof(type));
        }

        private static bool TryGetSelectedDefinition(
            SerializedProperty property,
            out IPEGAKnowledgeFactDefinition definition)
        {
            definition = null;
            SerializedProperty id = property.FindPropertyRelative("factDefinitionId");
            if (id == null || !PEGAKnowledgeFactDefinitionCatalog.TryGet(id.stringValue, out definition))
            {
                return false;
            }

            return IsSelectable(definition);
        }

        private static bool IsSelectable(IPEGAKnowledgeFactDefinition definition) =>
            definition != null && definition.AllowsDesignerAuthoredInitialFact &&
            PEGAKnowledgeFactAuthoringPayload.Supports(definition.AddressType) &&
            PEGAKnowledgeFactAuthoringPayload.Supports(definition.ValueType);

        private static void ShowDefinitionMenu(SerializedProperty property)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                return;
            }

            GenericMenu menu = new GenericMenu();
            IPEGAKnowledgeFactDefinition[] definitions = PEGAKnowledgeFactDefinitionCatalog.All
                .OrderBy(definition => definition.Name, StringComparer.Ordinal)
                .ToArray();
            SerializedProperty selectedId = property.FindPropertyRelative("factDefinitionId");
            for (int index = 0; index < definitions.Length; index++)
            {
                IPEGAKnowledgeFactDefinition definition = definitions[index];
                if (IsSelectable(definition))
                {
                    menu.AddItem(
                        new GUIContent(FormatDefinition(definition)),
                        string.Equals(selectedId.stringValue, definition.Id, StringComparison.Ordinal),
                        () => SelectDefinition(property.serializedObject.targetObject, property.propertyPath, definition.Id));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent(
                        $"{FormatDefinition(definition)} — {GetUnavailabilityMessage(definition)}"));
                }
            }

            if (definitions.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("Nenhuma definição serializável disponível"));
            }

            menu.ShowAsContext();
        }

        private static void SelectDefinition(UnityEngine.Object host, string propertyPath, string definitionId)
        {
            if (host == null)
            {
                return;
            }

            Undo.RecordObject(host, "Choose initial Knowledge fact definition");
            var serializedObject = new SerializedObject(host);
            serializedObject.Update();
            SerializedProperty entry = serializedObject.FindProperty(propertyPath);
            if (entry == null)
            {
                return;
            }

            entry.FindPropertyRelative("factDefinitionId").stringValue = definitionId;
            ResetPayload(entry.FindPropertyRelative("address"));
            ResetPayload(entry.FindPropertyRelative("value"));
            serializedObject.ApplyModifiedProperties();
        }

        private static void ResetPayload(SerializedProperty payload)
        {
            string[] strings = { "actorIdText", "actorProfileIdText", "stringValue" };
            for (int index = 0; index < strings.Length; index++)
            {
                payload.FindPropertyRelative(strings[index]).stringValue = string.Empty;
            }

            payload.FindPropertyRelative("actorDeclaration").objectReferenceValue = null;
            payload.FindPropertyRelative("actorProfile").objectReferenceValue = null;
            payload.FindPropertyRelative("objectEntryId").FindPropertyRelative("objectEntryIdText").stringValue = string.Empty;
            payload.FindPropertyRelative("integerValue").intValue = 0;
            payload.FindPropertyRelative("longValue").longValue = 0L;
            payload.FindPropertyRelative("floatValue").floatValue = 0f;
            payload.FindPropertyRelative("doubleValue").doubleValue = 0d;
            payload.FindPropertyRelative("boolValue").boolValue = false;
            payload.FindPropertyRelative("vector3Value").vector3Value = Vector3.zero;

            SerializedProperty presence = payload.FindPropertyRelative("perceivedAreaPresenceValue");
            presence.FindPropertyRelative("isPresent").boolValue = false;
            presence.FindPropertyRelative("observerPosition").vector3Value = Vector3.zero;
            presence.FindPropertyRelative("perceivedAreaRadiusMeters").floatValue = 0f;
        }

        private static void DrawIdentityStatus(Rect rect, string id, string identityName)
        {
            string text = string.IsNullOrWhiteSpace(id)
                ? $"Selecione uma identidade {identityName} válida."
                : $"Identidade lógica persistida: {id}";
            EditorGUI.LabelField(rect, text, EditorStyles.miniLabel);
        }

        private static bool TryGetActorId(ActorDeclaration declaration, out ActorId actorId, out string diagnostic)
        {
            actorId = default;
            if (declaration == null)
            {
                diagnostic = "Select an Actor Declaration.";
                return false;
            }

            if (!declaration.TryCreateDescriptor(
                    "PEGA.ActorGroup.InitialKnowledge", out ActorDescriptor descriptor, out ActorSetIssue issue))
            {
                diagnostic = issue.Message;
                return false;
            }

            actorId = descriptor.ActorId;
            diagnostic = string.Empty;
            return true;
        }

        private static string FormatDefinition(IPEGAKnowledgeFactDefinition definition) =>
            $"{definition.Name} ({definition.AddressType.Name} → {definition.ValueType.Name})";

        private static string GetUnavailabilityMessage(string definitionId)
        {
            if (!PEGAKnowledgeFactDefinitionCatalog.TryGet(definitionId, out IPEGAKnowledgeFactDefinition definition))
            {
                return $"A definição '{definitionId}' não está disponível. A entrada não foi remapeada.";
            }

            return GetUnavailabilityMessage(definition);
        }

        private static string GetUnavailabilityMessage(IPEGAKnowledgeFactDefinition definition)
        {
            if (!definition.AllowsDesignerAuthoredInitialFact)
            {
                return "reservada às fontes autorizadas";
            }

            if (!PEGAKnowledgeFactAuthoringPayload.Supports(definition.AddressType))
            {
                return $"formato de endereço não suportado ({definition.AddressType.Name})";
            }

            if (!PEGAKnowledgeFactAuthoringPayload.Supports(definition.ValueType))
            {
                return $"formato de valor não suportado ({definition.ValueType.Name})";
            }

            return "indisponível";
        }
    }
}
