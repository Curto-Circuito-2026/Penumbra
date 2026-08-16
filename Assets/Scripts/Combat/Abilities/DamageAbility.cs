using UnityEngine;

[CreateAssetMenu(fileName = "NewDamageAbility", menuName = "Combat/Abilities/Damage Ability")]
public class DamageAbility : Ability
{
    public enum AbilityType { SingleTarget, AoE, Projectile }

    [Header("Efeitos da Habilidade")]
    [Tooltip("Tipo da habilidade: Alvo único, Área de Efeito (AoE) ou Projétil.")]
    [SerializeField] private AbilityType type = AbilityType.SingleTarget;

    [Tooltip("Raio de efeito no caso de dano em área (AoE).")]
    [SerializeField] private float aoeRadius = 3.5f;

    [Tooltip("Prefab do projétil (se tipo for Projétil).")]
    [SerializeField] private GameObject projectilePrefab;

    [Tooltip("Efeito visual / VFX gerado no impacto/local do conjuração.")]
    [SerializeField] private GameObject vfxPrefab;

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        Vector3 casterPos = caster != null ? caster.transform.position : targetPosition;
        Vector3 direction = (targetPosition - casterPos).normalized;

        bool hasVFX = CombatVisualEffects.Instance != null;

        string nameUpper = abilityName.ToUpper();
        bool isMeteor = nameUpper.Contains("METEOR") || nameUpper.Contains("ULTIMATE") || nameUpper.Contains("FÚRIA") || nameUpper.Contains("FURIA") || damage >= 50f;
        bool isFrost = nameUpper.Contains("FROST") || nameUpper.Contains("GELO") || nameUpper.Contains("NOVA") || nameUpper.Contains("AVANÇO") || nameUpper.Contains("AVANCO");

        if (hasVFX)
        {
            if (isMeteor)
            {
                // Visual R: Meteoro caindo dos céus + Explosão Massiva + Shake
                CombatVisualEffects.Instance.PlayAbilityRMeteorStrike(targetPosition, () =>
                {
                    ApplyDamage(caster, targetPosition, targetEntity, direction);
                });
                return true;
            }
            else if (isFrost || (type == AbilityType.AoE && !isMeteor))
            {
                // Visual E: Nova de Gelo / Onda de Choque Expansiva
                CombatVisualEffects.Instance.PlayAbilityEFrostNova(targetPosition, aoeRadius);
                ApplyDamage(caster, targetPosition, targetEntity, direction);
                return true;
            }
            else
            {
                // Visual Q: Bola de Fogo / Projétil Flamejante com Explosão
                CombatVisualEffects.Instance.PlayAbilityQFireball(casterPos, targetPosition, () =>
                {
                    ApplyDamage(caster, targetPosition, targetEntity, direction);
                });
                return true;
            }
        }
        else
        {
            // Fallback sem VFX Manager
            ApplyDamage(caster, targetPosition, targetEntity, direction);
            return true;
        }
    }

    private void ApplyDamage(GameObject caster, Vector3 targetPosition, GameObject targetEntity, Vector3 direction)
    {
        if (type == AbilityType.AoE)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(targetPosition, aoeRadius);
            int count = 0;
            foreach (Collider2D hit in hits)
            {
                if (hit.gameObject == caster) continue;
                if (hit.TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(damage, (hit.transform.position - targetPosition).normalized);
                    count++;
                }
            }
            Debug.Log($"[DamageAbility] Habilidade AoE '{abilityName}' atingiu {count} alvos em raio {aoeRadius}");
        }
        else
        {
            if (targetEntity != null && targetEntity.TryGetComponent(out IDamageable targetDmg))
            {
                targetDmg.TakeDamage(damage, direction);
                Debug.Log($"[DamageAbility] Habilidade '{abilityName}' causou {damage} de dano a {targetEntity.name}");
            }
            else
            {
                Collider2D hitCol = Physics2D.OverlapCircle(targetPosition, 0.8f);
                if (hitCol != null && hitCol.gameObject != caster && hitCol.TryGetComponent(out IDamageable hitDmg))
                {
                    hitDmg.TakeDamage(damage, direction);
                }
            }
        }

        SpawnVFX(targetPosition);
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
