using UnityEngine;

/// <summary>
/// Componente simples para girar objetos/projéteis continuamente no eixo Z.
/// Usado na pedra arremessada pelo Mapinguari para dar o efeito de pedra rodando no ar.
/// </summary>
public class SpinningObject : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private bool clockwise = true;

    private void Update()
    {
        float dir = clockwise ? -1f : 1f;
        transform.Rotate(0f, 0f, dir * rotationSpeed * Time.deltaTime);
    }
}
