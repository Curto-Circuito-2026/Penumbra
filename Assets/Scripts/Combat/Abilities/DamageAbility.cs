using UnityEngine;

[CreateAssetMenu(fileName = "NewDamageAbility", menuName = "Combat/Abilities/Damage Ability")]
public class DamageAbility : Ability
{
    public enum AbilityType { SingleTarget, AoE, Projectile }

    [Header("Efeitos da Habilidade")]
    [Tooltip("Tipo da habilidade: Alvo único, Área de Efeito (AoE) ou Projétil.")]
    [SerializeField] private AbilityType type = AbilityType.SingleTarget;

    [Tooltip("Raio de efeito no caso de dano em área (AoE).")]
    [SerializeField] private float aoeRadius = 3f;

    [Tooltip("Prefab do projétil (se tipo for Projétil).")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Efeito visual / VFX gerado no impacto/local do conjuração.")]
    [SerializeField] private GameObject vfxPrefab;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        Vector3 casterPos = caster != null ? caster.transform.position : targetPosition;
        Vector3 direction = (targetPosition - casterPos).normalized;

        switch (type)
        {
            case AbilityType.SingleTarget:
                if (targetEntity != null)
                {
                    IDamageable damageable = targetEntity.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(damage, direction);
                        Debug.Log($"[DamageAbility] Habilidade '{abilityName}' causou {damage} de dano a {targetEntity.name}");
                        SpawnVFX(targetEntity.transform.position);
                        return true;
                    }
                }
                // Se não tinha entidade alvo direto, pode aplicar dano se houver collider no ponto
                Collider2D hitCol = Physics2D.OverlapCircle(targetPosition, 0.8f);
                if (hitCol != null && hitCol.gameObject != caster)
                {
                    IDamageable damageable = hitCol.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(damage, direction);
                        SpawnVFX(targetPosition);
                        return true;
                    }
                }
                SpawnVFX(targetPosition);
                return true;

            case AbilityType.AoE:
                Collider2D[] hits = Physics2D.OverlapCircleAll(targetPosition, aoeRadius);
                int count = 0;
                foreach (Collider2D hit in hits)
                {
                    if (hit.gameObject == caster) continue;
                    IDamageable damageable = hit.GetComponent<IDamageable>();
                    if (damageable != null)
                    {
                        damageable.TakeDamage(damage, (hit.transform.position - targetPosition).normalized);
                        count++;
                    }
                }
                Debug.Log($"[DamageAbility] AoE '{abilityName}' atingiu {count} alvos em raio {aoeRadius}");
                SpawnVFX(targetPosition);
                return true;

            case AbilityType.Projectile:
                if (projectilePrefab != null)
                {
                    GameObject proj = Instantiate(projectilePrefab, casterPos + direction * 0.5f, Quaternion.identity);
                    // Se o projétil tiver Rigidbody2D, aplica velocidade
                    Rigidbody2D projRb = proj.GetComponent<Rigidbody2D>();
                    if (projRb != null)
                    {
                        projRb.linearVelocity = direction * 12f;
                    }
                }
                else
                {
                    // Fallback projétil instantâneo
                    if (targetEntity != null && targetEntity.TryGetComponent(out IDamageable targetDmg))
                    {
                        targetDmg.TakeDamage(damage, direction);
                    }
                }
                SpawnVFX(targetPosition);
                return true;
        }

        return false;
    }

    private void SpawnVFX(Vector3 position)
    {
        if (vfxPrefab != null)
        {
            GameObject vfx = Instantiate(vfxPrefab, position, Quaternion.identity);
            Destroy(vfx, 2f);
        }
    }
}
