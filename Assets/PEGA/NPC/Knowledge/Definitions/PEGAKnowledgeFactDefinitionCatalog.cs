using System;
using System.Collections.Generic;
using System.Reflection;

namespace PEGA.NPC
{
    /// <summary>Índice transitório das definições canônicas de fatos declaradas pelo PEGA.</summary>
    /// <remarks>
    /// Declare cada propriedade como campo estático readonly que implemente IPEGAKnowledgeFactDefinition
    /// em Knowledge/Definitions. A descoberta é automática e IDs duplicados são rejeitados.
    /// </remarks>
    public static class PEGAKnowledgeFactDefinitionCatalog
    {
        private static readonly Dictionary<string, IPEGAKnowledgeFactDefinition> Definitions = Build();

        public static IReadOnlyCollection<IPEGAKnowledgeFactDefinition> All => Definitions.Values;

        public static bool TryGet(string id, out IPEGAKnowledgeFactDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(id.Trim(), out definition);
        }

        private static Dictionary<string, IPEGAKnowledgeFactDefinition> Build()
        {
            var definitions = new Dictionary<string, IPEGAKnowledgeFactDefinition>(StringComparer.Ordinal);
            Type[] types = typeof(PEGAKnowledgeFactDefinitionCatalog).Assembly.GetTypes();
            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                FieldInfo[] fields = types[typeIndex].GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (!typeof(IPEGAKnowledgeFactDefinition).IsAssignableFrom(field.FieldType) ||
                        field.GetValue(null) is not IPEGAKnowledgeFactDefinition definition)
                    {
                        continue;
                    }

                    if (definitions.ContainsKey(definition.Id))
                    {
                        throw new InvalidOperationException(
                            $"Fact definition id '{definition.Id}' is declared more than once.");
                    }

                    definitions.Add(definition.Id, definition);
                }
            }

            return definitions;
        }
    }
}
