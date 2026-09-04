using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Gerencia os efeitos visuais e telegrafias de perigo para os Chefes:
/// - Faixas de perigo em grade Hashtag (#).
/// - Sombras no chão telegrafando a queda de meteoros/bolas de fogo.
/// </summary>
public class BossTelegraphVisuals : MonoBehaviour
{
    private static BossTelegraphVisuals instance;
    public static BossTelegraphVisuals Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<BossTelegraphVisuals>();
                if (instance == null)
                {
                    GameObject obj = new GameObject("[BossTelegraphVisuals]");
                    instance = obj.AddComponent<BossTelegraphVisuals>();
                }
            }
            return instance;
        }
    }

    private Sprite circleSprite;
    private Sprite squareSprite;

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);

        circleSprite = CreateCircleSprite();
        squareSprite = CreateSquareSprite();
    }

    #region 1. Telegrafia de Grade Hashtag (#)
    /// <summary>
    /// Cria uma linha/faixa telegrafada de perigo na arena por uma duração.
    /// </summary>
    public GameObject CreateDangerLine(Vector3 start, Vector3 end, float width, float duration, Color color)
    {
        GameObject lineObj = new GameObject("VFX_DangerLine");
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        lr.startWidth = width;
        lr.endWidth = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = color;
        lr.endColor = color;

        StartCoroutine(AnimateDangerLine(lr, lineObj, duration, color));
        return lineObj;
    }

    private IEnumerator AnimateDangerLine(LineRenderer lr, GameObject obj, float duration, Color baseColor)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float pulse = 0.4f + Mathf.Sin(elapsed * 15f) * 0.35f;
            Color c = new Color(baseColor.r, baseColor.g, baseColor.b, pulse);
            if (lr != null)
            {
                lr.startColor = c;
                lr.endColor = c;
            }
            yield return null;
        }

        if (obj != null) Destroy(obj);
    }

    /// <summary>
    /// Deixa um rastro de fogo no chão após uma investida que causa dano em área.
    /// </summary>
    public void CreateFireTrail(Vector3 start, Vector3 end, float width, float duration, float damage, LayerMask targetMask)
    {
        StartCoroutine(AnimateFireTrail(start, end, width, duration, damage, targetMask));
    }

    /// <summary>
    /// Aplica dano ao jogador de forma universal procurando CharacterController2D e PlayerStats em qualquer camada.
    /// </summary>
    public static bool TryDamagePlayer(Vector3 pos, float radius, float damage, Vector3 knockbackDir, bool allowDashImmunity)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(pos, radius);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            // Ignora o Boss e projéteis
            if (hit.GetComponentInParent<BoitataBossController>() != null) continue;

            CharacterController2D cc = hit.GetComponent<CharacterController2D>() ?? hit.GetComponentInParent<CharacterController2D>();
            PlayerStats ps = hit.GetComponent<PlayerStats>() ?? hit.GetComponentInParent<PlayerStats>();

            if (cc != null || ps != null || hit.CompareTag("Player"))
            {
                // Se a habilidade permite esquiva por Dash e o jogador está no meio do Dash
                if (allowDashImmunity && cc != null && cc.IsDashing)
                {
                    // Desviou com Dash! Sem dano!
                    return false;
                }

                IDamageable dmg = (IDamageable)ps ?? hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
                if (dmg != null && !(dmg is BoitataBossController))
                {
                    dmg.TakeDamage(damage, knockbackDir);
                    return true;
                }
            }
        }
        return false;
    }

    private IEnumerator AnimateFireTrail(Vector3 start, Vector3 end, float width, float duration, float damage, LayerMask targetMask)
    {
        GameObject trailObj = new GameObject("VFX_FireTrail");
        LineRenderer lr = trailObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 8;
        lr.startWidth = width;
        lr.endWidth = width * 0.8f;
        lr.material = new Material(Shader.Find("Sprites/Default"));

        Color startColor = new Color(1f, 0.45f, 0.05f, 0.85f);
        Color endColor = new Color(1f, 0.15f, 0f, 0.6f);
        lr.startColor = startColor;
        lr.endColor = endColor;

        for (int i = 0; i < 8; i++)
        {
            float t = (float)i / 7f;
            lr.SetPosition(i, Vector3.Lerp(start, end, t));
        }

        float elapsed = 0f;
        float damageTickTimer = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            damageTickTimer += Time.deltaTime;

            float fade = 1f - (elapsed / duration);
            Color c = Color.Lerp(new Color(1f, 0.1f, 0f, 0f), startColor, fade);
            lr.startColor = c;
            lr.endColor = c;

            // Causa dano por tick no jogador se pisar no fogo
            if (damageTickTimer >= 0.25f)
            {
                damageTickTimer = 0f;
                for (int p = 0; p <= 6; p++)
                {
                    Vector3 samplePos = Vector3.Lerp(start, end, p / 6f);
                    if (TryDamagePlayer(samplePos, width * 0.7f, damage * 0.3f, (end - start).normalized, allowDashImmunity: false))
                    {
                        break;
                    }
                }
            }

            yield return null;
        }

        Destroy(trailObj);
    }

    [Header("Modular Serpent Sprites")]
    [SerializeField] private Sprite[] serpentHeadSprites;
    [SerializeField] private Sprite serpentBodyStraightSprite;
    [SerializeField] private Sprite serpentBodyCurveSprite;
    [SerializeField] private Sprite[] serpentTailSprites;

    private void EnsureSerpentSpritesLoaded()
    {
        if (serpentHeadSprites != null && serpentHeadSprites.Length > 0 &&
            serpentBodyStraightSprite != null && serpentBodyCurveSprite != null &&
            serpentTailSprites != null && serpentTailSprites.Length > 0)
        {
            return;
        }

        List<Sprite> heads = new List<Sprite>();
        List<Sprite> tails = new List<Sprite>();

#if UNITY_EDITOR
        var loaded = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Boitata/boitata_spritesheet.png").OfType<Sprite>();
        foreach (var s in loaded)
        {
            if (s == null) continue;
            string n = s.name.ToLower();
            if (n.Contains("head") || n == "boitata_spritesheet_7" || n == "boitata_spritesheet_11" || n.Contains("mod_head")) heads.Add(s);
            else if (n.Contains("straight") || n == "boitata_spritesheet_5" || n == "boitata_mod_body_straight") serpentBodyStraightSprite = s;
            else if (n.Contains("curve") || n == "boitata_spritesheet_3" || n == "boitata_mod_body_curve") serpentBodyCurveSprite = s;
            else if (n.Contains("tail") || n == "boitata_spritesheet_4" || n == "boitata_spritesheet_2" || n == "boitata_spritesheet_6" || n.Contains("mod_tail")) tails.Add(s);
        }
#else
        Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in allSprites)
        {
            if (s == null) continue;
            string n = s.name.ToLower();
            if (n.Contains("head") || n == "boitata_spritesheet_7" || n == "boitata_spritesheet_11" || n.Contains("mod_head")) heads.Add(s);
            else if (n.Contains("straight") || n == "boitata_spritesheet_5" || n == "boitata_mod_body_straight") serpentBodyStraightSprite = s;
            else if (n.Contains("curve") || n == "boitata_spritesheet_3" || n == "boitata_mod_body_curve") serpentBodyCurveSprite = s;
            else if (n.Contains("tail") || n == "boitata_spritesheet_4" || n == "boitata_spritesheet_2" || n == "boitata_spritesheet_6" || n.Contains("mod_tail")) tails.Add(s);
        }
#endif

        if (heads.Count > 0) serpentHeadSprites = heads.ToArray();
        if (tails.Count > 0) serpentTailSprites = tails.ToArray();
    }

    /// <summary>
    /// Spawna uma serpente modular segmentada do Boitatá com suporte a curvas em waypoints.
    /// No ponto da curva, o gomo correspondente utiliza boitata_mod_body_curve perfeitamente alinhado.
    /// </summary>
    public void SpawnFireSerpentDash(Vector3[] waypoints, float moveSpeed, float damage, LayerMask playerMask)
    {
        EnsureSerpentSpritesLoaded();

        GameObject serpentObj = new GameObject("ModularBoitataSerpent");
        ModularBoitataSerpent serpent = serpentObj.AddComponent<ModularBoitataSerpent>();
        serpent.SetSprites(serpentHeadSprites, serpentBodyStraightSprite, serpentBodyCurveSprite, serpentTailSprites);

        float calculatedSpeed = moveSpeed > 0f ? moveSpeed : 15.5f;
        serpent.Launch(waypoints, calculatedSpeed, damage);
    }

    public void SpawnFireSerpentDash(Vector3 start, Vector3 end, float moveSpeed, float damage, LayerMask playerMask)
    {
        SpawnFireSerpentDash(new Vector3[] { start, end }, moveSpeed, damage, playerMask);
    }
    #endregion

    #region 2. Telegrafia de Sombra & Queda de Meteoro
    /// <summary>
    /// Gera uma sombra no chão que cresce e escurece, seguida por uma bola de fogo caindo do céu.
    /// </summary>
    public void SpawnMeteorWithShadow(Vector3 targetPos, float warningDuration, float impactRadius, float damage, LayerMask targetMask)
    {
        StartCoroutine(AnimateMeteorWithShadow(targetPos, warningDuration, impactRadius, damage, targetMask));
    }

    private IEnumerator AnimateMeteorWithShadow(Vector3 targetPos, float warningDuration, float radius, float damage, LayerMask targetMask)
    {
        // 1. Sombra no Chão
        GameObject shadowObj = new GameObject("VFX_GroundShadow");
        shadowObj.transform.position = targetPos;
        SpriteRenderer shadowSr = shadowObj.AddComponent<SpriteRenderer>();
        shadowSr.sprite = circleSprite ?? CreateCircleSprite();
        shadowSr.color = new Color(0f, 0f, 0f, 0.1f);
        shadowSr.sortingOrder = 1;
        shadowObj.transform.localScale = Vector3.zero;

        // 2. Círculo de Alerta Vermelho
        GameObject dangerCircle = new GameObject("VFX_DangerCircle");
        dangerCircle.transform.position = targetPos;
        SpriteRenderer dangerSr = dangerCircle.AddComponent<SpriteRenderer>();
        dangerSr.sprite = circleSprite ?? CreateCircleSprite();
        dangerSr.color = new Color(1f, 0.25f, 0.1f, 0.25f);
        dangerSr.sortingOrder = 2;
        dangerCircle.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);

        // 3. Bola de Fogo Caindo do Céu
        GameObject meteor = new GameObject("VFX_FallingMeteor");
        Vector3 skyOrigin = targetPos + new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 9f, 0f);
        meteor.transform.position = skyOrigin;

        SpriteRenderer meteorSr = meteor.AddComponent<SpriteRenderer>();
        meteorSr.sprite = circleSprite ?? CreateCircleSprite();
        meteorSr.color = new Color(1f, 0.5f, 0.1f, 1f);
        meteorSr.sortingOrder = 15;
        meteor.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        TrailRenderer trail = meteor.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.startWidth = 0.5f;
        trail.endWidth = 0.05f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1f, 0.6f, 0.1f, 0.95f);
        trail.endColor = new Color(1f, 0.1f, 0f, 0f);

        float elapsed = 0f;
        float finalShadowScale = radius * 1.8f;

        while (elapsed < warningDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / warningDuration;

            // Sombra cresce e escurece
            float currentShadowSize = Mathf.Lerp(0.2f, finalShadowScale, t);
            shadowObj.transform.localScale = new Vector3(currentShadowSize, currentShadowSize * 0.6f, 1f);
            shadowSr.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.15f, 0.65f, t));

            // Pulso no anel de perigo
            float pulse = 0.2f + Mathf.Sin(elapsed * 12f) * 0.15f;
            dangerSr.color = new Color(1f, 0.2f, 0.05f, pulse);

            // Meteoro desce acelerando em direção ao chão
            meteor.transform.position = Vector3.Lerp(skyOrigin, targetPos, t * t);

            yield return null;
        }

        // 4. Impacto e Explosão
        Destroy(shadowObj);
        Destroy(dangerCircle);
        Destroy(meteor);

        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayExplosionVFX(targetPos, new Color(1f, 0.4f, 0.1f, 1f), new Color(1f, 0.85f, 0.2f, 1f), radius * 1.6f);
            CombatVisualEffects.Instance.TriggerCameraShake(0.15f, 0.18f);
        }

        // Aplica dano universal na área de impacto
        TryDamagePlayer(targetPos, radius * 1.25f, damage, Vector3.up, allowDashImmunity: false);
    }
    #endregion

    #region 3. Super 360 de Bolas de Fogo (com esquiva de Dash)
    /// <summary>
    /// Spawna um anel de bolas de fogo em 360 graus que viajam até as bordas da tela.
    /// O jogador pode usar o Dash para passar por baixo das bolas de fogo sem tomar dano!
    /// </summary>
    public void Spawn360FireballRing(Vector3 origin, int fireballCount, float speed, float damage, LayerMask playerMask)
    {
        StartCoroutine(Animate360FireballRing(origin, fireballCount, speed, damage, playerMask));
    }

    private IEnumerator Animate360FireballRing(Vector3 origin, int count, float speed, float damage, LayerMask playerMask)
    {
        List<GameObject> fireballs = new List<GameObject>();
        List<Vector3> directions = new List<Vector3>();

        float angleStep = 360f / count;
        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            directions.Add(dir);

            GameObject fb = new GameObject("VFX_Fireball_360");
            fb.transform.position = origin + dir * 0.8f;
            SpriteRenderer sr = fb.AddComponent<SpriteRenderer>();
            sr.sprite = circleSprite ?? CreateCircleSprite();
            sr.color = new Color(1f, 0.45f, 0.1f, 1f);
            sr.sortingOrder = 12;
            fb.transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            TrailRenderer trail = fb.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = 0.5f;
            trail.endWidth = 0.05f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.65f, 0.15f, 0.95f);
            trail.endColor = new Color(1f, 0.15f, 0f, 0f);

            fireballs.Add(fb);
        }

        float maxLifetime = 3.8f;
        float elapsed = 0f;
        HashSet<int> destroyed = new HashSet<int>();

        while (elapsed < maxLifetime)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < fireballs.Count; i++)
            {
                if (destroyed.Contains(i)) continue;
                GameObject fb = fireballs[i];
                if (fb == null) { destroyed.Add(i); continue; }

                fb.transform.position += directions[i] * (speed * Time.deltaTime);

                // Checa colisão com o jogador de forma infalível
                if (TryDamagePlayer(fb.transform.position, 0.85f, damage, directions[i], allowDashImmunity: true))
                {
                    if (CombatVisualEffects.Instance != null)
                    {
                        CombatVisualEffects.Instance.PlayImpactBurst(fb.transform.position, new Color(1f, 0.4f, 0.1f), 1.2f);
                    }
                    Destroy(fb);
                    destroyed.Add(i);
                }
            }

            yield return null;
        }

        foreach (var fb in fireballs)
        {
            if (fb != null) Destroy(fb);
        }
    }
    #endregion

    #region 4. Catavento de Chamas 360° (Feixes em Cruz Giratórios)
    /// <summary>
    /// Cria 4 feixes de fogo contínuos em cruz que giram 360 graus forçando o jogador a correr ao redor do Boss.
    /// </summary>
    public IEnumerator AnimateSpinningFireBeamsRoutine(Transform centerTransform, int beamCount, float length, float spinDuration, float totalRotations, float damage, LayerMask playerMask, Func<Vector3> positionProvider = null)
    {
        List<GameObject> beamObjects = new List<GameObject>();
        List<LineRenderer> lineRenderers = new List<LineRenderer>();

        float angleStep = 360f / beamCount;
        for (int i = 0; i < beamCount; i++)
        {
            GameObject bObj = new GameObject($"VFX_FireBeam_{i}");
            LineRenderer lr = bObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 6;
            lr.startWidth = 0.9f;
            lr.endWidth = 1.4f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1f, 0.9f, 0.3f, 0.95f);
            lr.endColor = new Color(1f, 0.25f, 0.05f, 0.8f);

            beamObjects.Add(bObj);
            lineRenderers.Add(lr);
        }

        float elapsed = 0f;
        float damageTickTimer = 0f;

        Vector3 currentOrigin = positionProvider != null 
            ? positionProvider() 
            : (centerTransform != null ? centerTransform.position : Vector3.zero);

        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            damageTickTimer += Time.deltaTime;

            float t = elapsed / spinDuration;
            float currentAngle = t * 360f * totalRotations;

            Vector3 targetOrigin = positionProvider != null 
                ? positionProvider() 
                : (centerTransform != null ? centerTransform.position : Vector3.zero);

            // Desliza suavemente a origem em direção ao rabo com MoveTowards contínuo
            currentOrigin = Vector3.MoveTowards(currentOrigin, targetOrigin, 6.0f * Time.deltaTime);
            Vector3 origin = currentOrigin;

            for (int i = 0; i < beamCount; i++)
            {
                float angle = (currentAngle + i * angleStep) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                Vector3 endPoint = origin + dir * length;

                LineRenderer lr = lineRenderers[i];
                if (lr != null)
                {
                    for (int p = 0; p < 6; p++)
                    {
                        float pt = (float)p / 5f;
                        Vector3 pos = Vector3.Lerp(origin, endPoint, pt);
                        pos += Vector3.Cross(dir, Vector3.forward) * (Mathf.Sin(Time.time * 25f + p) * 0.12f);
                        lr.SetPosition(p, pos);
                    }
                }

                // Causa dano por tick ao longo do feixe de fogo
                if (damageTickTimer >= 0.2f)
                {
                    for (int p = 1; p <= 8; p++)
                    {
                        Vector3 samplePos = origin + dir * (length * (p / 8f));
                        if (TryDamagePlayer(samplePos, 0.75f, damage * 0.25f, dir, allowDashImmunity: false))
                        {
                            break;
                        }
                    }
                }
            }

            if (damageTickTimer >= 0.2f) damageTickTimer = 0f;

            yield return null;
        }

        foreach (var b in beamObjects)
        {
            if (b != null) Destroy(b);
        }
    }
    #endregion

    #region Sprites Procedurais
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

    private Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        Color[] cols = new Color[16 * 16];
        for (int i = 0; i < cols.Length; i++) cols[i] = Color.white;
        tex.SetPixels(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
    }
    #endregion
}
