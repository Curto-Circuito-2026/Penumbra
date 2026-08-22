using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using TMPro;

/// <summary>
/// Gerenciador de Efeitos Visuais (VFX) e Retorno Visual para o sistema de combate 2D.
/// Gera proceduralmente:
/// - LMB: Arco de corte corpo a corpo (Melee Slash) e faíscas de impacto.
/// - RMB: Projétil de energia guiado (Ranged Bolt) com explosão de impacto.
/// - Q: Bola de Fogo / Projéteil flamejante com explosão de fogo em área.
/// - E: Nova de Gelo / Onda de choque gélida expansiva.
/// - R (Ultimate): Retículo no chão, meteoro caindo dos céus e explosão massiva.
/// - Texto de Dano Flutuante (Floating Combat Text) para todos os ataques.
/// - Tremor de Câmera (Camera Shake).
/// </summary>
public class CombatVisualEffects : MonoBehaviour
{
    private static CombatVisualEffects instance;

    public static CombatVisualEffects Instance
    {
        get
        {
            if (instance == null)
            {
                instance = UnityEngine.Object.FindAnyObjectByType<CombatVisualEffects>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("[CombatVisualEffects]");
                    instance = obj.AddComponent<CombatVisualEffects>();
                }
            }
            return instance;
        }
    }

    [SerializeField] private Sprite projectileSprite;
    private Camera mainCamera;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        mainCamera = Camera.main;
    }

    private void Start()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    #region 1. LMB - Ataque Melee (Arco de Corte)
    /// <summary>
    /// Gera um arco visual de corte (slash arc) na direção do ataque Melee.
    /// </summary>
    public void PlayMeleeSlash(Vector3 origin, Vector3 direction)
    {
        StartCoroutine(AnimateMeleeSlash(origin, direction));
    }

    private IEnumerator AnimateMeleeSlash(Vector3 origin, Vector3 direction)
    {
        GameObject slashObj = new GameObject("VFX_MeleeSlash");
        slashObj.transform.position = origin;

        LineRenderer line = slashObj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 16;
        line.startWidth = 0.22f;
        line.endWidth = 0.02f;
        line.material = new Material(Shader.Find("Sprites/Default"));

        Color startCol = new Color(1f, 1f, 1f, 0.95f);
        Color endCol = new Color(0.88f, 0.96f, 1f, 0.35f);
        line.startColor = startCol;
        line.endColor = endCol;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float sweepArc = 110f;
        float radius = 1.6f;

        float duration = 0.15f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;

            float currentSweep = Mathf.Lerp(-sweepArc / 2f, sweepArc / 2f, t);

            for (int i = 0; i < 16; i++)
            {
                float stepT = (float)i / 15f;
                float angleDeg = baseAngle + currentSweep * stepT;
                float rad = angleDeg * Mathf.Deg2Rad;

                Vector3 pos = origin + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius;
                line.SetPosition(i, pos);
            }

            Color c = Color.Lerp(startCol, new Color(1f, 1f, 1f, 0f), t);
            line.startColor = c;
            line.endColor = new Color(0.88f, 0.96f, 1f, (1f - t) * 0.35f);

            yield return null;
        }

        Destroy(slashObj);
    }
    #endregion

    #region 2. RMB - Ataque Ranged (Projétil Naia)
    /// <summary>
    /// Spawna um projétil voando até o alvo e gerando impacto ao colidir.
    /// </summary>
    public void PlayRangedProjectile(Vector3 origin, Vector3 targetPos, Action onImpact = null)
    {
        StartCoroutine(AnimateRangedBolt(origin, targetPos, onImpact));
    }

    private Sprite GetProjectileSprite()
    {
        if (projectileSprite != null) return projectileSprite;

#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("naia_projectile t:Texture2D");
        if (guids.Length > 0)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
            Sprite[] sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length > 0)
            {
                projectileSprite = sprites[0];
                return projectileSprite;
            }
        }
#endif
        var loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in loadedSprites)
        {
            if (s.name.Contains("naia_projectile"))
            {
                projectileSprite = s;
                break;
            }
        }

        return projectileSprite ?? CreateCircleSprite();
    }

    private IEnumerator AnimateRangedBolt(Vector3 origin, Vector3 targetPos, Action onImpact)
    {
        GameObject bolt = new GameObject("VFX_RangedBolt");
        bolt.transform.position = origin;

        Vector3 flightDir = (targetPos - origin).normalized;
        if (flightDir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(flightDir.y, flightDir.x) * Mathf.Rad2Deg;
            bolt.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        SpriteRenderer sr = bolt.AddComponent<SpriteRenderer>();
        sr.sprite = GetProjectileSprite();
        sr.color = Color.white;
        sr.sortingOrder = 10;
        bolt.transform.localScale = new Vector3(1f, 1f, 1f);

        TrailRenderer trail = bolt.AddComponent<TrailRenderer>();
        trail.time = 0.15f;
        trail.startWidth = 0.18f;
        trail.endWidth = 0.0f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(0.7f, 0.95f, 1f, 0.8f);
        trail.endColor = new Color(0.3f, 0.7f, 1f, 0f);

        float speed = 20f;
        float dist = Vector3.Distance(origin, targetPos);
        float duration = Mathf.Max(0.05f, dist / speed);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            bolt.transform.position = Vector3.Lerp(origin, targetPos, t);
            yield return null;
        }

        onImpact?.Invoke();
        PlayImpactBurst(targetPos, new Color(0.4f, 0.85f, 1f, 1f), 1.2f);
        Destroy(bolt, 0.1f);
    }
    #endregion

    #region 3. Q - Habilidade Q (Bola de Fogo / Fireball)
    /// <summary>
    /// Lança um projétil de fogo que explode em área no ponto de impacto.
    /// </summary>
    public void PlayAbilityQFireball(Vector3 origin, Vector3 targetPos, Action onImpact = null)
    {
        StartCoroutine(AnimateFireball(origin, targetPos, onImpact));
    }

    private IEnumerator AnimateFireball(Vector3 origin, Vector3 targetPos, Action onImpact)
    {
        GameObject fireball = new GameObject("VFX_Fireball");
        fireball.transform.position = origin;

        SpriteRenderer sr = fireball.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.4f, 0.1f, 1f);
        fireball.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        TrailRenderer trail = fireball.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = 0.5f;
        trail.endWidth = 0.05f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1f, 0.5f, 0.1f, 0.95f);
        trail.endColor = new Color(1f, 0.1f, 0f, 0f);

        float speed = 15f;
        float dist = Vector3.Distance(origin, targetPos);
        float duration = dist / speed;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            fireball.transform.position = Vector3.Lerp(origin, targetPos, t);
            yield return null;
        }

        onImpact?.Invoke();
        PlayExplosionVFX(targetPos, new Color(1f, 0.4f, 0.1f, 1f), new Color(1f, 0.8f, 0.2f, 1f), 2.2f);
        TriggerCameraShake(0.18f, 0.15f);
        Destroy(fireball, 0.15f);
    }
    #endregion

    #region 4. E - Habilidade E (Nova de Gelo / Frost Nova)
    /// <summary>
    /// Gera uma onda de choque congelante expansiva ao redor da posição.
    /// </summary>
    public void PlayAbilityEFrostNova(Vector3 position, float radius = 3.5f)
    {
        StartCoroutine(AnimateFrostNova(position, radius));
    }

    private IEnumerator AnimateFrostNova(Vector3 position, float maxRadius)
    {
        GameObject nova = new GameObject("VFX_FrostNova");
        nova.transform.position = position;

        LineRenderer line = nova.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 36;
        line.startWidth = 0.25f;
        line.endWidth = 0.25f;
        line.material = new Material(Shader.Find("Sprites/Default"));

        Color startColor = new Color(0.3f, 0.9f, 1f, 0.95f);
        Color endColor = new Color(0.8f, 0.95f, 1f, 0.2f);
        line.startColor = startColor;
        line.endColor = endColor;

        float duration = 0.4f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float currentRadius = Mathf.Lerp(0.2f, maxRadius, Mathf.Sin(t * Mathf.PI * 0.5f));

            for (int i = 0; i <= 35; i++)
            {
                float angle = (i / 35f) * Mathf.PI * 2f;
                Vector3 p = position + new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f);
                line.SetPosition(i, p);
            }

            Color alphaCol = Color.Lerp(startColor, new Color(0.2f, 0.8f, 1f, 0f), t * t);
            line.startColor = alphaCol;
            line.endColor = alphaCol;

            yield return null;
        }

        Destroy(nova);
    }
    #endregion

    #region Efeito Aquático (Water Burst)
    /// <summary>
    /// Gera uma onda/explosão de água azul e espirros translúcidos.
    /// </summary>
    public void PlayWaterBurst(Vector3 position, float radius = 2.0f)
    {
        StartCoroutine(AnimateWaterBurst(position, radius));
    }

    private IEnumerator AnimateWaterBurst(Vector3 position, float maxRadius)
    {
        GameObject waterObj = new GameObject("VFX_WaterBurst");
        waterObj.transform.position = position;

        LineRenderer line = waterObj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 36;
        line.startWidth = 0.22f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));

        Color startColor = new Color(0.2f, 0.7f, 1f, 0.95f);
        line.startColor = startColor;
        line.endColor = new Color(0.1f, 0.4f, 0.9f, 0.2f);

        float duration = 0.35f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            float currentRadius = Mathf.Lerp(0.1f, maxRadius, Mathf.Sin(t * Mathf.PI * 0.5f));

            for (int i = 0; i <= 35; i++)
            {
                float angle = (i / 35f) * Mathf.PI * 2f;
                Vector3 p = position + new Vector3(Mathf.Cos(angle) * currentRadius, Mathf.Sin(angle) * currentRadius, 0f);
                line.SetPosition(i, p);
            }

            Color alphaCol = Color.Lerp(startColor, new Color(0.1f, 0.5f, 1f, 0f), t * t);
            line.startColor = alphaCol;
            line.endColor = alphaCol;

            yield return null;
        }

        Destroy(waterObj);
    }
    #endregion

    #region 5. R - Habilidade R (Ultimate / Meteoro Devastador)
    /// <summary>
    /// Gera a animação da Ultimate: Retículo de mira -> Queda do Meteoro -> Explosão Massiva + Shake.
    /// </summary>
    public void PlayAbilityRMeteorStrike(Vector3 targetPos, Action onImpact = null)
    {
        StartCoroutine(AnimateMeteorStrike(targetPos, onImpact));
    }

    private IEnumerator AnimateMeteorStrike(Vector3 targetPos, Action onImpact)
    {
        // 1. Retículo no chão
        GameObject reticle = new GameObject("VFX_MeteorReticle");
        reticle.transform.position = targetPos;

        LineRenderer line = reticle.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 32;
        line.startWidth = 0.15f;
        line.endWidth = 0.15f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = new Color(1f, 0.85f, 0.1f, 0.9f);
        line.endColor = new Color(1f, 0.2f, 0.1f, 0.9f);

        float radius = 3.5f;
        for (int i = 0; i < 32; i++)
        {
            float angle = (i / 31f) * Mathf.PI * 2f;
            line.SetPosition(i, targetPos + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        yield return new WaitForSeconds(0.25f);

        // 2. Queda do Meteoro
        Vector3 spawnPos = targetPos + new Vector3(3f, 10f, 0f);
        GameObject meteor = new GameObject("VFX_Meteor");
        meteor.transform.position = spawnPos;

        SpriteRenderer sr = meteor.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.9f, 0.2f, 1f);
        meteor.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

        TrailRenderer trail = meteor.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 1.0f;
        trail.endWidth = 0.1f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1f, 0.8f, 0.1f, 1f);
        trail.endColor = new Color(1f, 0.1f, 0f, 0f);

        float dropDuration = 0.35f;
        float elapsed = 0f;

        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / dropDuration;
            meteor.transform.position = Vector3.Lerp(spawnPos, targetPos, t * t);
            yield return null;
        }

        onImpact?.Invoke();

        // 3. Explosão do Meteoro
        Destroy(reticle);
        Destroy(meteor);

        PlayExplosionVFX(targetPos, new Color(1f, 0.85f, 0.1f, 1f), new Color(1f, 0.2f, 0.1f, 1f), 4.5f);
        TriggerCameraShake(0.35f, 0.35f);
    }
    #endregion

    #region Utilitários de Explosão, Impacto e Texto Flutuante
    public void PlayImpactBurst(Vector3 position, Color color, float size = 1.5f)
    {
        StartCoroutine(AnimateBurst(position, color, size, 0.2f));
    }

    public void PlayExplosionVFX(Vector3 position, Color innerColor, Color outerColor, float maxRadius = 3f)
    {
        StartCoroutine(AnimateBurst(position, innerColor, maxRadius, 0.35f));
        StartCoroutine(AnimateBurst(position, outerColor, maxRadius * 0.7f, 0.25f));
    }

    private IEnumerator AnimateBurst(Vector3 position, Color color, float targetSize, float duration)
    {
        GameObject burst = new GameObject("VFX_Burst");
        burst.transform.position = position;

        SpriteRenderer sr = burst.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = color;
        burst.transform.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float currentScale = Mathf.Lerp(0f, targetSize, Mathf.Sin(t * Mathf.PI * 0.5f));
            burst.transform.localScale = new Vector3(currentScale, currentScale, 1f);

            sr.color = Color.Lerp(color, new Color(color.r, color.g, color.b, 0f), t * t);
            yield return null;
        }

        Destroy(burst);
    }

    /// <summary>
    /// Spawna um número de dano flutuante no mundo na posição indicada.
    /// </summary>
    public void SpawnFloatingText(Vector3 position, string text, Color color, float fontSize = 4.5f)
    {
        StartCoroutine(AnimateFloatingText(position, text, color, fontSize));
    }

    private IEnumerator AnimateFloatingText(Vector3 position, string text, Color color, float fontSize)
    {
        GameObject textObj = new GameObject("VFX_FloatingText");
        textObj.transform.position = position + new Vector3(UnityEngine.Random.Range(-0.3f, 0.3f), 0.5f, 0f);

        TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = 100;

        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }

        Vector3 startPos = textObj.transform.position;
        Vector3 endPos = startPos + new Vector3(0f, 1.2f, 0f);

        float duration = 0.75f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            textObj.transform.position = Vector3.Lerp(startPos, endPos, Mathf.Sin(t * Mathf.PI * 0.5f));
            tmp.color = Color.Lerp(color, new Color(color.r, color.g, color.b, 0f), t * t);

            yield return null;
        }

        Destroy(textObj);
    }

    /// <summary>
    /// Aplica tremor leve na Câmera Principal.
    /// </summary>
    public void TriggerCameraShake(float duration = 0.2f, float intensity = 0.15f)
    {
        StartCoroutine(AnimateCameraShake(duration, intensity));
    }

    private IEnumerator AnimateCameraShake(float duration, float intensity)
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) yield break;

        Vector3 origPos = mainCamera.transform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * intensity * (1f - (elapsed / duration));
            mainCamera.transform.localPosition = new Vector3(origPos.x + randomOffset.x, origPos.y + randomOffset.y, origPos.z);
            yield return null;
        }

        mainCamera.transform.localPosition = origPos;
    }

    private Sprite CreateCircleSprite()
    {
        Texture2D tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color[] cols = new Color[32 * 32];
        Vector2 center = new Vector2(15.5f, 15.5f);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= 15f)
                {
                    float alpha = Mathf.Clamp01(1f - (dist / 15f));
                    cols[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    cols[y * 32 + x] = Color.clear;
                }
            }
        }

        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }
    #endregion
}
