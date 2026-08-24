using System.Collections.Generic;
using UnityEngine;
using static CombatVisualEffects;
using static Unity.Collections.Unicode;

[CreateAssetMenu(fileName = "NewDamageAbility", menuName = "Combat/Abilities/Damage Ability")]
public class DamageAbility : Ability
{
    public enum AbilityType { SingleTarget, AoE, Projectile, Scattershot, Grenade }

    [Header("Efeitos da Habilidade")]
    [Tooltip("Tipo da habilidade: Alvo único, Área de Efeito (AoE) ou Projétil.")]
    [SerializeField] private AbilityType type = AbilityType.SingleTarget;

    [Tooltip("Raio de efeito no caso de dano em área (AoE).")]
    [SerializeField] private float aoeRadius = 3.5f;

    [Tooltip("Prefab ou sprite do projétil (se tipo for Projétil).")]
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField] private Sprite projectileSprite;

    [Tooltip("Efeito visual / VFX gerado no impacto/local do conjuração.")]
    [SerializeField] private GameObject vfxPrefab;

    [Header("Configurações do Scattershot")]
    [Tooltip("Quantidade de projéteis (pellets) disparados.")]
    [SerializeField] private int pelletCount = 5;
    [Tooltip("Ângulo total de dispersão (cone) em graus.")]
    [SerializeField] private float spreadAngle = 45f;
    [Tooltip("Alcance máximo do tiro de espingarda.")]
    [SerializeField] private float scatterRange = 8f;

    [Header("Configurações da Granada")]
    [Tooltip("Altura máxima do arco da granada (falso 3D).")]
    [SerializeField] private float grenadeArcHeight = 2.5f;
    [Tooltip("Tempo em segundos que a granada leva para cair no chão.")]
    [SerializeField] private float grenadeFlightTime = 0.8f;

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

        if (type == AbilityType.Scattershot)
        {
            ApplyScatterDamage(caster, casterPos, direction);
            return true;
        }
        else if (type == AbilityType.Grenade) 
        {
            ApplyGrenadeDamage(caster, casterPos, targetPosition);
            return true;
        }

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

    private void ApplyGrenadeDamage(GameObject caster, Vector3 startPos, Vector3 targetPosition)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Grenade precisa de um Projectile Prefab assinalado no Inspector!");
            return;
        }

        Vector3 direction = targetPosition - startPos;
        if (direction.magnitude > range)
        {
            targetPosition = startPos + (direction.normalized * range);
        }

        GameObject grenadeObj = Instantiate(projectilePrefab, startPos, Quaternion.identity);

        if (!grenadeObj.TryGetComponent<ProjectileThrow>(out var grenadeScript))
        {
            grenadeScript = grenadeObj.AddComponent<ProjectileThrow>();
        }

        // Initialize it with the arc parameters and the AoE radius!
        grenadeScript.Initialize(caster, startPos, targetPosition, damage, aoeRadius, vfxPrefab, grenadeArcHeight, grenadeFlightTime, projectileSprite);
    }

    private void ApplyScatterDamage(GameObject caster, Vector3 startPos, Vector3 baseDirection)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Scattershot precisa de um Projectile Prefab assinalado no Inspector!");
            return;
        }

        float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
        float angleStep = pelletCount > 1 ? spreadAngle / (pelletCount - 1) : 0;
        float currentAngle = baseAngle - (spreadAngle / 2f);

        for (int i = 0; i < pelletCount; i++)
        {
            float dirX = Mathf.Cos(currentAngle * Mathf.Deg2Rad);
            float dirY = Mathf.Sin(currentAngle * Mathf.Deg2Rad);
            Vector3 pelletDir = new Vector3(dirX, dirY, 0).normalized;

            GameObject projObj = Instantiate(projectilePrefab, startPos, Quaternion.identity);

            if (!projObj.TryGetComponent<Projectile>(out var projScript))
            {
                projScript = projObj.AddComponent<Projectile>();
            }

            projScript.Initialize(pelletDir, caster, damage, 0, projectileSprite);

            currentAngle += angleStep;
        }

    }

    private bool IsCasterOrAlly(GameObject obj, GameObject caster)
    {
        if (obj == null) return true;
        if (caster != null && (obj == caster || obj.transform.IsChildOf(caster.transform) || caster.transform.IsChildOf(obj.transform) || obj.transform.root == caster.transform.root)) return true;

        // Se o conjurador for o Player, nunca causa dano no próprio Player
        if (caster != null && (caster.CompareTag("Player") || caster.GetComponentInParent<CharacterController2D>() != null || caster.GetComponentInParent<PlayerStats>() != null))
        {
            if (obj.CompareTag("Player") || obj.GetComponentInParent<CharacterController2D>() != null || obj.GetComponentInParent<PlayerStats>() != null)
            {
                return true;
            }
        }
        // Se o conjurador for um Inimigo, nunca causa dano em aliados inimigos
        if (caster != null && (caster.CompareTag("Enemy") || caster.GetComponentInParent<EnemyStats>() != null))
        {
            if (obj.CompareTag("Enemy") || obj.GetComponentInParent<EnemyStats>() != null)
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
