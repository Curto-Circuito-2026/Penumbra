using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossTrigger : ICinematicClip
{
    Actor main;
    Actor enemy;

    public GameObject Boss;
    public string BossSubtitle;
    public float battleCameraZoom = 8f;
    public float bossZoom = 14f;
    [Tooltip("Offset vertical/horizontal para centralizar a câmera perfeitamente no Boss durante a apresentação.")]
    public Vector2 cameraOffset = new Vector2(0f, 1.5f);

    [Header("Música de Batalha")]
    [Tooltip("Música de batalha exclusiva para este Boss (opcional - auto-detectado se nulo).")]
    [SerializeField] public AudioClip bossMusicClip;
    [Tooltip("Tempo em segundos da transição suave da música de fase para a do Boss.")]
    [SerializeField] public float bossMusicFadeDuration = 2.2f;

    public override void BindActors()
    {
        GameObject playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
        if (playerObj != null)
        {
            main = playerObj.GetComponent<Actor>();
            if (main == null) main = playerObj.AddComponent<Actor>();
        }

        if (Boss == null)
        {
            Boss = GameObject.Find("Boss_Boitata") ??
                   GameObject.Find("Boss_Mapinguari") ??
                   GameObject.Find("Boss_Matinta") ??
                   GameObject.Find("Boss_Cuca") ??
                   (transform.parent != null ? transform.parent.GetComponentInChildren<MatintaBossController>()?.gameObject : null) ??
                   (transform.parent != null ? transform.parent.GetComponentInChildren<MapinguariBossController>()?.gameObject : null) ??
                   (transform.parent != null ? transform.parent.GetComponentInChildren<CucaBossController>()?.gameObject : null) ??
                   (transform.parent != null ? transform.parent.GetComponentInChildren<BoitataBossController>()?.gameObject : null) ??
                   UnityEngine.Object.FindAnyObjectByType<MatintaBossController>()?.gameObject ??
                   UnityEngine.Object.FindAnyObjectByType<MapinguariBossController>()?.gameObject ??
                   UnityEngine.Object.FindAnyObjectByType<CucaBossController>()?.gameObject ??
                   UnityEngine.Object.FindAnyObjectByType<BoitataBossController>()?.gameObject ??
                   GameObject.FindWithTag("Enemy");
        }

        if (Boss != null)
        {
            enemy = Boss.GetComponent<Actor>();
            if (enemy == null)
            {
                enemy = Boss.AddComponent<Actor>();
                enemy.actorName = Boss.name.Replace("Boss_", "").Replace("(Clone)", "").Trim();
            }
        }
    }

    public override IEnumerator Play()
    {
        if (main == null || enemy == null)
        {
            BindActors();
        }

        if (enemy == null)
        {
            Debug.LogWarning("[BossTrigger] Boss não encontrado para a Cutscene!");
            EndCutsceneSafely();
            yield break;
        }

        // Inicia a transição imediata para a música épica do Boss antes/durante a apresentação
        PlayBossBGM();

        // Garante que o CameraManager está pronto
        if (parent != null && parent.camManager == null)
        {
            Camera c = parent.cam ?? Camera.main;
            if (c != null) parent.camManager = c.GetComponent<CameraManager>();
        }

        if (enemy == null)
        {
            EndCutsceneSafely();
            yield break;
        }

        // Calcula o centro visual do Boss (ao invés do pivô no chão/pés)
        Vector3 bossCenter = enemy.transform.position + (Vector3)cameraOffset;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.bounds.size.y > 0.5f)
        {
            bossCenter = sr.bounds.center;
        }

        Vector3 bossCamOffset = new Vector3(bossCenter.x - enemy.transform.position.x, bossCenter.y - enemy.transform.position.y, -10f);

        if (parent != null && parent.camManager != null)
        {
            parent.camManager.Zoom(battleCameraZoom, 1f);
            yield return parent.camManager.Move(bossCenter, 2f).ToYieldInstruction();

            if (enemy == null)
            {
                EndCutsceneSafely();
                yield break;
            }

            parent.camManager.SetTarget(enemy.transform, bossCamOffset);
            parent.camManager.Zoom(bossZoom, 1f);
        }

        if (parent != null && enemy != null)
        {
            parent.ShowTitle(enemy.actorName, BossSubtitle);
        }

        if (parent != null && parent.camManager != null)
        {
            yield return parent.camManager.Shake(0.25f, 2f).ToYieldInstruction();
        }

        yield return new WaitForSeconds(1.2f);

        if (parent != null && parent.camManager != null)
        {
            parent.camManager.Zoom(battleCameraZoom, 1f);
        }

        while ((enemy != null && enemy.moving) || (main != null && main.moving))
        {
            yield return null;
        }

        EndCutsceneSafely();
    }

    private void EndCutsceneSafely()
    {
        if (parent != null && parent.camManager != null && main != null)
        {
            parent.camManager.SetTarget(main.transform, new Vector3(0f, 0f, -10f));
        }

        if (parent != null && parent.gameStateManager != null)
        {
            parent.gameStateManager.SetState(GameState.Playing);
        }
        else if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Playing);
        }

        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    private void PlayBossBGM()
    {
        if (AudioController.Instance == null) return;

        AudioClip clip = bossMusicClip;

#if UNITY_EDITOR
        if (clip == null)
        {
            string bName = enemy != null && !string.IsNullOrEmpty(enemy.actorName) 
                ? enemy.actorName.ToLower() 
                : (Boss != null ? Boss.name.ToLower() : "");

            // 1 = Pântano / Matinta Perera
            if (bName.Contains("matinta"))
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Boss/Boss 1.mp3");
            }
            // 2 = Mata Atlântica / Boitatá
            else if (bName.Contains("boitata"))
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Boss/Boss 2.mp3");
            }
            // 3 = Cidade Destruída / Mapinguari
            else if (bName.Contains("mapinguari"))
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Boss/Boss 3.mp3");
            }
            // 4 = Boss Final / Cuca
            else if (bName.Contains("cuca"))
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Boss/Boss 4.mp3");
            }
        }
#endif

        if (clip != null)
        {
            AudioController.Instance.PlayBGM(clip, fadeDuration: bossMusicFadeDuration, loop: true);
        }
    }
}
