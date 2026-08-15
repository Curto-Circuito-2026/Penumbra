using UnityEngine;

/// <summary>
/// Indicador visual sutil (anel/retículo de mira) exibido sobre o inimigo focado ou selecionado.
/// </summary>
public class TargetSelectionRing : MonoBehaviour
{
    private LineRenderer lineRenderer;
    [SerializeField] private int segments = 32;
    [SerializeField] private float radius = 0.8f;
    [SerializeField] private float pulseSpeed = 4f;

    private Transform currentTarget;
    private Color activeColor = new Color(1f, 0.2f, 0.2f, 0.85f); // Vermelho radiante de alvo

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = segments + 1;
        lineRenderer.startWidth = 0.05f;
        lineRenderer.endWidth = 0.05f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = activeColor;
        lineRenderer.endColor = activeColor;
        lineRenderer.enabled = false;
    }

    private void Update()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        // Posiciona e pulsa suavemente o anel ao redor do alvo
        Vector3 center = currentTarget.position;
        float pulseRadius = radius + Mathf.Sin(Time.time * pulseSpeed) * 0.05f;
        float angleStep = 360f / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle) * pulseRadius, Mathf.Sin(angle) * pulseRadius, 0f);
            lineRenderer.SetPosition(i, pos);
        }
    }

    public void ShowOnTarget(Transform target, Color color)
    {
        currentTarget = target;
        activeColor = color;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.enabled = true;
    }

    public void Hide()
    {
        currentTarget = null;
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }
}
