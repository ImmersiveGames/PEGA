using System;
using System.Collections.Generic;
using System.Reflection;
namespace PEGA.NPC.Knowledge.Definitions
{
    /// <summary>Índice transitório das definições canônicas de fatos declaradas pelo PEGA.</summary>
    /// <remarks>
    /// Declare cada propriedade como campo estático readonly que implemente IPEGAKnowledgeFactDefinition
    /// em Knowledge/Definitions. A descoberta é automática e IDs duplicados são rejeitados.
    /// </remarks>
    public static class PegaKnowledgeFactDefinitionCatalog
    {
        private static readonly Dictionary<string, IPegaKnowledgeFactDefinition> Definitions = Build();

        public static IReadOnlyCollection<IPegaKnowledgeFactDefinition> All => Definitions.Values;

        public static bool TryGet(string id, out IPegaKnowledgeFactDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                definition = null;
                return false;
            }

            return Definitions.TryGetValue(id.Trim(), out definition);
        }

        private static Dictionary<string, IPegaKnowledgeFactDefinition> Build()
        {
            var definitions = new Dictionary<string, IPegaKnowledgeFactDefinition>(StringComparer.Ordinal);
            Type[] types = typeof(PegaKnowledgeFactDefinitionCatalog).Assembly.GetTypes();
            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                FieldInfo[] fields = types[typeIndex].GetFields(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    if (!typeof(IPegaKnowledgeFactDefinition).IsAssignableFrom(field.FieldType) ||
                        field.GetValue(null) is not IPegaKnowledgeFactDefinition definition)
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
