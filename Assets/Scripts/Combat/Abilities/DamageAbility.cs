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

    private Vector3 GetCastSpawnPosition(GameObject caster, Vector3 direction)
    {
        if (caster == null) return Vector3.zero;

        // Elevação do tronco/ombro da Naia a partir dos pés (altura da mão)
        Vector3 spawnPos = caster.transform.position + new Vector3(0f, 0.55f, 0f);

        // Deslocamento para a mão lateral conforme a direção do disparo
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            // Lateral (Direita ou Esquerda)
            spawnPos += new Vector3(Mathf.Sign(direction.x) * 0.35f, 0f, 0f);
        }
        else if (direction.y > 0)
        {
            // Cima
            spawnPos += new Vector3(0.12f, 0.15f, 0f);
        }
        else
        {
            // Baixo
            spawnPos += new Vector3(-0.1f, -0.1f, 0f);
        }

        return spawnPos;
    }

    public override bool Cast(GameObject caster, Vector3 targetPosition, GameObject targetEntity)
    {
        Vector3 rawDirection = (targetPosition - (caster != null ? caster.transform.position : targetPosition)).normalized;
        if (rawDirection.sqrMagnitude < 0.001f) rawDirection = Vector3.right;

        Vector3 casterPos = caster != null ? GetCastSpawnPosition(caster, rawDirection) : targetPosition;
        Vector3 direction = (targetPosition - casterPos).normalized;
        if (direction.sqrMagnitude < 0.001f) direction = rawDirection;

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
                // Visual Q: Bola de Fogo / Projétil Flamejante saindo da mão com Explosão no alvo
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

    private bool IsCasterOrAlly(GameObject obj, GameObject caster)
    {
        if (obj == null) return true;
        if (caster != null && (obj == caster || obj.transform.IsChildOf(caster.transform) || caster.transform.IsChildOf(obj.transform))) return true;

        // Se o conjurador for o Player, nunca causa dano no próprio Player
        if (caster != null && (caster.CompareTag("Player") || caster.GetComponentInParent<CharacterController2D>() != null))
        {
            if (obj.CompareTag("Player") || obj.GetComponentInParent<CharacterController2D>() != null || obj.GetComponentInParent<PlayerStats>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private void ApplyDamage(GameObject caster, Vector3 targetPosition, GameObject targetEntity, Vector3 direction)
    {
        float impactRadius = (type == AbilityType.AoE) ? aoeRadius : 1.3f;
        Collider2D[] hits = Physics2D.OverlapCircleAll(targetPosition, impactRadius);
        int count = 0;

        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            if (IsCasterOrAlly(hit.gameObject, caster)) continue;

            IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (damageable != null && !(damageable is CharacterController2D) && !(damageable is PlayerStats))
            {
                damageable.TakeDamage(damage, direction);
                count++;

                // Se for SingleTarget, atinge apenas o primeiro alvo válido dentro do raio de impacto
                if (type == AbilityType.SingleTarget)
                {
                    break;
                }
            }
        }
        Debug.Log($"[DamageAbility] Habilidade '{abilityName}' atingiu {count} alvos em raio {impactRadius} no ponto {targetPosition}");

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
