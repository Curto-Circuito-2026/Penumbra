using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Serpente modular do Boitatá com movimentação contínua real e fluida ao longo do trajeto:
/// - Cada gomo (cabeça, corpo e cauda) amostra uma trajetória contínua com arco de curva suave no vértice W1.
/// - A rotação varia suavemente a cada frame (Mathf.LerpAngle), eliminando qualquer sensação de 'troca' ou 'snap'.
/// - Passo de encaixe: segmentSpacing = 0.73f (35px / 48 PPU), com ordenação decrescente de camadas (50 - i).
/// </summary>
public class ModularBoitataSerpent : MonoBehaviour
{
    [Header("Sprites Modulares")]
    [SerializeField] private Sprite[] headSprites;
    [SerializeField] private Sprite bodyStraightSprite;
    [SerializeField] private Sprite bodyCurveSprite;
    [SerializeField] private Sprite[] tailSprites;

    private List<Transform> segments = new List<Transform>();
    private List<SpriteRenderer> segmentRenderers = new List<SpriteRenderer>();

    private Vector3[] waypoints;
    private float totalDistance;
    private float traveledDistance;
    private float speed = 15.5f;
    private float damage = 30f;
    private float segmentSpacing = 0.73f;
    private int segmentCount = 20;
    private bool isMoving;
    private float headFlameTimer;
    private float tailFlameTimer;

    public void SetSprites(Sprite[] heads, Sprite bodyStraight, Sprite bodyCurve, Sprite[] tails)
    {
        if (heads != null && heads.Length > 0) headSprites = heads;
        if (bodyStraight != null) bodyStraightSprite = bodyStraight;
        if (bodyCurve != null) bodyCurveSprite = bodyCurve;
        if (tails != null && tails.Length > 0) tailSprites = tails;
    }

    public void Launch(Vector3[] pathWaypoints, float moveSpeed, float attackDamage)
    {
        if (pathWaypoints == null || pathWaypoints.Length < 2) return;

        waypoints = pathWaypoints;
        speed = moveSpeed > 0 ? moveSpeed : 11.5f;
        damage = attackDamage;

        totalDistance = 0f;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            totalDistance += Vector3.Distance(waypoints[i], waypoints[i + 1]);
        }

        segmentCount = Mathf.Clamp(Mathf.RoundToInt(totalDistance / segmentSpacing), 10, 40);

        BuildSerpent();
        isMoving = true;
    }

    public void Launch(Vector3 start, Vector3 end, float moveSpeed, float attackDamage, int numSegments = 0)
    {
        Launch(new Vector3[] { start, end }, moveSpeed, attackDamage);
    }

    private void BuildSerpent()
    {
        transform.position = waypoints[0];

        Vector3 initDir = (waypoints[1] - waypoints[0]).normalized;
        float moveAngle = Mathf.Atan2(initDir.y, initDir.x) * Mathf.Rad2Deg;
        float headAngle = moveAngle + 90f;
        float bodyAngle = moveAngle - 90f;
        float tailAngle = moveAngle;

        // 1. Cabeça (Segmento 0)
        GameObject headObj = new GameObject("Serpent_Head");
        headObj.transform.SetParent(transform);
        headObj.transform.position = waypoints[0];
        headObj.transform.rotation = Quaternion.Euler(0f, 0f, headAngle);
        headObj.transform.localScale = Vector3.one;

        SpriteRenderer headSr = headObj.AddComponent<SpriteRenderer>();
        if (headSprites != null && headSprites.Length > 0) headSr.sprite = headSprites[0];
        headSr.sortingLayerName = "Default";
        headSr.sortingOrder = 50;
        segments.Add(headObj.transform);
        segmentRenderers.Add(headSr);

        // 2. Gomos de Corpo (Segmentos 1 a segmentCount) - todos com bodyStraightSprite
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject bodyObj = new GameObject($"Serpent_Body_{i}");
            bodyObj.transform.SetParent(transform);
            bodyObj.transform.position = waypoints[0] - initDir * (segmentSpacing * (i + 1));
            bodyObj.transform.rotation = Quaternion.Euler(0f, 0f, bodyAngle);
            bodyObj.transform.localScale = Vector3.one;

            SpriteRenderer bodySr = bodyObj.AddComponent<SpriteRenderer>();
            bodySr.sprite = bodyStraightSprite;
            bodySr.sortingLayerName = "Default";
            bodySr.sortingOrder = 49 - i;
            segments.Add(bodyObj.transform);
            segmentRenderers.Add(bodySr);
        }

        // 3. Cauda (Último Segmento)
        GameObject tailObj = new GameObject("Serpent_Tail");
        tailObj.transform.SetParent(transform);
        tailObj.transform.position = waypoints[0] - initDir * (segmentSpacing * (segmentCount + 1));
        tailObj.transform.rotation = Quaternion.Euler(0f, 0f, tailAngle);
        tailObj.transform.localScale = Vector3.one;

        SpriteRenderer tailSr = tailObj.AddComponent<SpriteRenderer>();
        if (tailSprites != null && tailSprites.Length > 0) tailSr.sprite = tailSprites[0];
        tailSr.sortingLayerName = "Default";
        tailSr.sortingOrder = 49 - segmentCount - 1;
        segments.Add(tailObj.transform);
        segmentRenderers.Add(tailSr);
    }

    private void Update()
    {
        if (!isMoving || segments.Count == 0 || waypoints == null || waypoints.Length < 2) return;

        // 1. Animação de Chamas da Cabeça
        if (headSprites != null && headSprites.Length > 1 && segmentRenderers.Count > 0)
        {
            headFlameTimer += Time.deltaTime * 10f;
            int frame = ((int)headFlameTimer) % headSprites.Length;
            segmentRenderers[0].sprite = headSprites[frame];
        }

        // 2. Animação de Chamas da Cauda
        if (tailSprites != null && tailSprites.Length > 0 && segmentRenderers.Count > 0)
        {
            tailFlameTimer += Time.deltaTime * 9f;
            int tailIndex;
            if (tailSprites.Length == 3)
            {
                int cycle = ((int)tailFlameTimer) % 4;
                tailIndex = (cycle == 3) ? 1 : cycle;
            }
            else
            {
                tailIndex = ((int)tailFlameTimer) % tailSprites.Length;
            }
            segmentRenderers[segmentRenderers.Count - 1].sprite = tailSprites[tailIndex];
        }

        // 3. Avanço contínuo da investida
        traveledDistance += speed * Time.deltaTime;

        // Posição da Cabeça (segmento 0)
        SampleSmoothPath(traveledDistance, out Vector3 headPos, out float headAngle);
        segments[0].position = headPos;
        segments[0].rotation = Quaternion.Euler(0f, 0f, headAngle + 90f);

        Vector3 headDir = Quaternion.Euler(0f, 0f, headAngle) * Vector3.right;
        BossTelegraphVisuals.TryDamagePlayer(headPos, 1.4f, damage, headDir, allowDashImmunity: false);

        // 4. Posicionamento contínuo real de cada gomo de corpo
        for (int i = 1; i < segments.Count - 1; i++)
        {
            float d = traveledDistance - i * segmentSpacing;
            SpriteRenderer sr = segmentRenderers[i];
            Transform t = segments[i];
            t.localScale = Vector3.one;
            sr.sprite = bodyStraightSprite;
            sr.flipX = false;
            sr.flipY = false;
            sr.enabled = true;

            SampleSmoothPath(d, out Vector3 segPos, out float segAngle);
            t.position = segPos;
            t.rotation = Quaternion.Euler(0f, 0f, segAngle - 90f);

            if (i < 6)
            {
                Vector3 segDir = Quaternion.Euler(0f, 0f, segAngle) * Vector3.right;
                BossTelegraphVisuals.TryDamagePlayer(t.position, 1.1f, damage * 0.6f, segDir, allowDashImmunity: false);
            }
        }

        // 5. Cauda (último segmento - segue a mesma trajetória suave)
        int tailIdx = segments.Count - 1;
        float tailSpacing = 0.14f;
        float tailDist = traveledDistance - (segmentCount * segmentSpacing + tailSpacing);
        SampleSmoothPath(tailDist, out Vector3 tailPos, out float tailAngle);
        segments[tailIdx].position = tailPos;
        segments[tailIdx].rotation = Quaternion.Euler(0f, 0f, tailAngle);

        // Destrói com fade out após a cauda cruzar todo o trajeto
        float totalPathLen = totalDistance + (segmentCount + 2) * segmentSpacing;
        if (traveledDistance >= totalPathLen)
        {
            StartCoroutine(FadeAndDestroy());
            isMoving = false;
        }
    }

    /// <summary>
    /// Amostra posição e rotação ao longo do trajeto com curva suave contínua Bézier no vértice.
    /// </summary>
    private void SampleSmoothPath(float dist, out Vector3 pos, out float angle)
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            pos = Vector3.zero;
            angle = 0f;
            return;
        }

        if (waypoints.Length < 3)
        {
            Vector3 start = waypoints[0];
            Vector3 end = waypoints[waypoints.Length - 1];
            Vector3 dir = (end - start).normalized;
            pos = start + dir * dist;
            angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            return;
        }

        Vector3 W0 = waypoints[0];
        Vector3 W1 = waypoints[1];
        Vector3 W2 = waypoints[2];
        Vector3 inDir = (W1 - W0).normalized;
        Vector3 outDir = (W2 - W1).normalized;
        float inAngle = Mathf.Atan2(inDir.y, inDir.x) * Mathf.Rad2Deg;
        float outAngle = Mathf.Atan2(outDir.y, outDir.x) * Mathf.Rad2Deg;
        float leg1Len = Vector3.Distance(W0, W1);

        float filletRadius = 0.70f; // Raio de arco suave no cotovelo
        float curveStartDist = leg1Len - filletRadius;
        float curveEndDist = leg1Len + filletRadius;

        if (dist < curveStartDist)
        {
            // Reta de entrada
            pos = W0 + inDir * dist;
            angle = inAngle;
        }
        else if (dist > curveEndDist)
        {
            // Reta de saída
            pos = W1 + outDir * (dist - leg1Len);
            angle = outAngle;
        }
        else
        {
            // Curva suave Bézier quadrática com interpolação contínua de ângulo
            float t = (dist - curveStartDist) / (curveEndDist - curveStartDist);
            Vector3 P0 = W1 - inDir * filletRadius;
            Vector3 P1 = W1;
            Vector3 P2 = W1 + outDir * filletRadius;

            pos = (1f - t) * (1f - t) * P0 + 2f * (1f - t) * t * P1 + t * t * P2;
            angle = Mathf.LerpAngle(inAngle, outAngle, t);
        }
    }

    private IEnumerator FadeAndDestroy()
    {
        float fadeTime = 0.2f;
        float elapsed = 0f;

        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);

            foreach (var sr in segmentRenderers)
            {
                if (sr != null)
                {
                    Color c = sr.color;
                    c.a = alpha;
                    sr.color = c;
                }
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
