using System.Collections;
using UnityEngine;

public class ProjectileThrow : MonoBehaviour
{
    private GameObject caster;
    private float damage;
    private float aoeRadius;
    private GameObject explosionVfx;

    public void Initialize(GameObject caster, Vector3 startPos, Vector3 targetPos, float damage, float aoeRadius, GameObject vfx, float arcHeight = 2f, float duration = 0.8f, Sprite customSprite = null)
    {
        this.caster = caster;
        this.damage = damage;
        this.aoeRadius = aoeRadius;
        this.explosionVfx = vfx;

        if (customSprite != null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = customSprite;
        }

        StartCoroutine(ThrowRoutine(startPos, targetPos, arcHeight, duration));
    }

    private IEnumerator ThrowRoutine(Vector3 start, Vector3 target, float arcHeight, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration; // goes from 0 to 1

            Vector3 currentPos = Vector3.Lerp(start, target, t);

            currentPos.y += Mathf.Sin(t * Mathf.PI) * arcHeight;

            transform.position = currentPos;

            transform.Rotate(0, 0, 360 * Time.deltaTime);

            yield return null;
        }

        transform.position = target;
        transform.rotation = Quaternion.identity;

        Explode(target);
    }

    private void Explode(Vector3 impactPoint)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(impactPoint, aoeRadius);
        foreach (Collider2D hit in hits)
        {
            if (IsCasterOrAlly(hit.gameObject)) continue;

            IDamageable damageable = hit.GetComponent<IDamageable>() ?? hit.GetComponentInParent<IDamageable>();
            if (damageable != null && !(damageable is CharacterController2D) && !(damageable is PlayerStats))
            {
                Vector3 pushDirection = (hit.transform.position - impactPoint).normalized;
                damageable.TakeDamage(damage, pushDirection);
            }
        }

        if (explosionVfx != null)
        {
            GameObject vfx = Instantiate(explosionVfx, impactPoint, Quaternion.identity);
            Destroy(vfx, 2f);
        }

        Destroy(gameObject);
    }

    private bool IsCasterOrAlly(GameObject obj)
    {
        if (obj == null) return true;
        if (caster != null && (obj == caster || obj.transform.IsChildOf(caster.transform) || caster.transform.IsChildOf(obj.transform) || obj.transform.root == caster.transform.root)) return true;

        if (caster != null && (caster.CompareTag("Player") || caster.GetComponentInParent<CharacterController2D>() != null || caster.GetComponentInParent<PlayerStats>() != null))
        {
            if (obj.CompareTag("Player") || obj.GetComponentInParent<CharacterController2D>() != null || obj.GetComponentInParent<PlayerStats>() != null) return true;
        }
        if (caster != null && (caster.CompareTag("Enemy") || caster.GetComponentInParent<EnemyStats>() != null))
        {
            if (obj.CompareTag("Enemy") || obj.GetComponentInParent<EnemyStats>() != null) return true;
        }
        return false;
    }
}