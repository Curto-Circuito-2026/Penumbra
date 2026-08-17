using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Componente responsável por desenhar múltiplos círculos de alcance simultâneos ao redor do jogador
/// (por exemplo, ao segurar uma tecla como SHIFT/TAB ou ao passar o mouse pelos botões de habilidades).
/// </summary>
public class MultiRangeIndicator : MonoBehaviour
{
    public struct RangeCircleData
    {
        public float radius;
        public Color color;
        public string label;

        public RangeCircleData(float radius, Color color, string label = "")
        {
            this.radius = radius;
            this.color = color;
            this.label = label;
        }
    }

    [SerializeField] private int segments = 40;
    [SerializeField] private float lineWidth = 0.05f;

    private List<LineRenderer> activeRenderers = new List<LineRenderer>();
    private Material defaultMaterial;

    private void Awake()
    {
        defaultMaterial = new Material(Shader.Find("Sprites/Default"));
    }

    /// <summary>
    /// Exibe uma lista de círculos de alcance ao redor da posição informada.
    /// </summary>
    public void DisplayRanges(Vector3 centerPosition, List<RangeCircleData> ranges)
    {
        EnsureRendererCount(ranges.Count);

        for (int r = 0; r < ranges.Count; r++)
        {
            LineRenderer lr = activeRenderers[r];
            lr.enabled = true;
            lr.startColor = ranges[r].color;
            lr.endColor = ranges[r].color;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            float angleStep = 360f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 pos = centerPosition + new Vector3(Mathf.Cos(angle) * ranges[r].radius, Mathf.Sin(angle) * ranges[r].radius, 0f);
                lr.SetPosition(i, pos);
            }
        }

        // Esconde renderers sobressalentes
        for (int r = ranges.Count; r < activeRenderers.Count; r++)
        {
            activeRenderers[r].enabled = false;
        }
    }

    /// <summary>
    /// Oculta todos os círculos de alcance.
    /// </summary>
    public void HideAll()
    {
        foreach (var lr in activeRenderers)
        {
            if (lr != null) lr.enabled = false;
        }
    }

    private void EnsureRendererCount(int count)
    {
        while (activeRenderers.Count < count)
        {
            GameObject obj = new GameObject($"RangeLine_{activeRenderers.Count}");
            obj.transform.SetParent(transform, false);
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = segments + 1;
            lr.material = defaultMaterial;
            lr.enabled = false;
            activeRenderers.Add(lr);
        }
    }
}
