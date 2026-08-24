using UnityEngine;
[CreateAssetMenu(fileName = "Ability_EspelhoD'Agua", menuName = "Praia Games/Habilidades/Iara/EspelhoDeAgua")]
public class WaterMirrorAbility : Ability
{
    [SerializeField] private GameObject particlePrefab;
    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        if (caster == null) return false;

        Vector3 startPosition = caster.transform.position;
        if (particlePrefab != null)
        {
            GameObject startVFX = Instantiate(particlePrefab, startPosition, Quaternion.identity);
            Destroy(startVFX, 1f);
        }

        Vector3 offset = targetPosition - startPosition;
        offset.y = 0f;

        if (offset.magnitude > range)
        {
            offset = offset.normalized * range;
        }

        Vector3 destination = startPosition + offset;
        caster.transform.position = destination;
        if (particlePrefab != null)
        {
            GameObject startVFX = Instantiate(particlePrefab, destination, Quaternion.identity);
            Destroy(startVFX, 1f);
        }
        return true;
    }


}
