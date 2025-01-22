using System.Collections.Generic;
using UnityEngine;

public class FadeObjectsURP : MonoBehaviour
{
    [Header("Configurações")]
    public Transform player; // O Transform do jogador
    public LayerMask obstacleLayer; // Layer dos objetos que podem bloquear a visão
    public float fadeDuration = 0.5f; // Tempo de transição para a transparência
    public float transparentAlpha = 0.3f; // O quão transparente o objeto fica

    private List<Renderer> fadedObjects = new List<Renderer>(); // Objetos atualmente transparentes
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>(); // Materiais originais para restauração

    void Update()
    {
        HandleTransparency();
    }

    void HandleTransparency()
    {
        // Resetar objetos que não estão mais bloqueando a visão
        ResetTransparency();

        // Fazer um Raycast da câmera para o jogador
        Vector3 cameraPosition = transform.position;
        Vector3 directionToPlayer = player.position - transform.position;
        float distanceToPlayer = Vector3.Distance(player.position, transform.position);

        // Detectar os objetos no caminho
        RaycastHit[] hits = Physics.RaycastAll(cameraPosition, directionToPlayer, distanceToPlayer, obstacleLayer);

        foreach (RaycastHit hit in hits)
        {
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer != null && !fadedObjects.Contains(renderer))
            {
                // Tornar o objeto transparente
                FadeOut(renderer);
                fadedObjects.Add(renderer);
            }
        }
    }

    void FadeOut(Renderer renderer)
    {
        if (!originalMaterials.ContainsKey(renderer))
        {
            // Guardar os materiais originais
            originalMaterials[renderer] = renderer.materials;
        }

        Material[] newMaterials = renderer.materials;
        foreach (Material mat in newMaterials)
        {
            // Verificar se o material usa o shader URP/Lit
            if (mat.shader.name == "Universal Render Pipeline/Lit")
            {
                // Configurar o material para o modo transparente
                mat.SetFloat("_Surface", 1); // 1 = Transparente
                mat.SetFloat("_ZWrite", 0); // Desativar escrita no ZBuffer
                mat.renderQueue = 3000; // Configurar a fila de renderização para transparência

                // Ajustar o alfa da cor base
                Color baseColor = mat.GetColor("_BaseColor");
                baseColor.a = transparentAlpha; // Definir o nível de transparência
                mat.SetColor("_BaseColor", baseColor);

                // Ativar palavras-chave necessárias para transparência
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
                mat.SetOverrideTag("RenderType", "Transparent");
            }
        }

        renderer.materials = newMaterials;
    }

    void ResetTransparency()
    {
        foreach (Renderer renderer in fadedObjects)
        {
            if (renderer != null && originalMaterials.ContainsKey(renderer))
            {
                // Restaurar os materiais originais
                Material[] originalMats = originalMaterials[renderer];
                renderer.materials = originalMats;

                // Restaurar as configurações do material original
                foreach (Material mat in originalMats)
                {
                    if (mat.shader.name == "Universal Render Pipeline/Lit")
                    {
                        // Restaurar o material para o estado Opaque
                        mat.SetFloat("_Surface", 0); // 0 = Opaque             
                        mat.SetFloat("_ZWrite", 1); // Reativar escrita no ZBuffer
                        mat.renderQueue = -1; // Deixar o Unity decidir a renderQueue padrão

                        // Restaurar palavras-chave
                        mat.EnableKeyword("_SURFACE_TYPE_OPAQUE");
                        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.SetOverrideTag("RenderType", "Opaque");

                        // Restaurar o alpha para 1 (opaco)
                        Color baseColor = mat.GetColor("_BaseColor");
                        baseColor.a = 1f; // Totalmente opaco
                        mat.SetColor("_BaseColor", baseColor);
                    }
                }
            }
        }

        // Limpar a lista de objetos transparentes
        fadedObjects.Clear();
    }
}
