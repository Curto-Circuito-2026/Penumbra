using UnityEngine;

/// <summary>
/// Garante que o Canvas Principal com toda a UI (HUD, Vida, Moedas, Pause, Loja, Boss)
/// seja instanciado automaticamente como Singleton persistente (DontDestroyOnLoad)
/// em qualquer cena executada, mesmo se o desenvolvedor der Play diretamente na fase.
/// </summary>
public static class GameUIBootstrap
{
    private const string GameCanvasResourcePath = "UI/GameCanvas";
    private const string FallbackPrefabPath = "Assets/Prefabs/UI/GameCanvas.prefab";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureUIInstantiatedOnPlay()
    {
        // Se UIManager já existe na cena (persistido ou pré-existente), não faz nada
        if (UIManager.Instance != null && UIManager.Instance.gameObject != null)
        {
            return;
        }

        // Verifica se já existe um GameCanvas ativo ou inativo
        GameCanvasMarker existingMarker = Object.FindAnyObjectByType<GameCanvasMarker>(FindObjectsInactive.Include);
        if (existingMarker != null)
        {
            return;
        }

        InstantiateGameCanvas();
    }

    /// <summary>
    /// Instancia o Prefab do GameCanvas completo na cena e garante DontDestroyOnLoad.
    /// </summary>
    public static GameObject InstantiateGameCanvas()
    {
        // 1. Tenta carregar de Resources
        GameObject prefab = Resources.Load<GameObject>(GameCanvasResourcePath);

#if UNITY_EDITOR
        // 2. Se não encontrar em Resources no Editor, carrega do caminho de AssetDatabase
        if (prefab == null)
        {
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPrefabPath);
        }
#endif

        if (prefab != null)
        {
            GameObject canvasInstance = Object.Instantiate(prefab);
            canvasInstance.name = "GameCanvas";

            if (Application.isPlaying)
            {
                Object.DontDestroyOnLoad(canvasInstance);
            }

            Debug.Log("[GameUIBootstrap] GameCanvas completo inicializado com sucesso (DontDestroyOnLoad)!");
            return canvasInstance;
        }
        else
        {
            Debug.LogWarning("[GameUIBootstrap] Prefab do GameCanvas não foi localizado em Resources ou Prefabs.");
            return null;
        }
    }
}
