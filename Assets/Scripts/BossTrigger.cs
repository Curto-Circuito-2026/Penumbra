using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossTrigger : ICinematicClip
{
    Actor main;
    Actor enemy;

    public GameObject Boss;
    public string BossSubtitle;
    public float battleCameraZoom = 5f;
    public float bossZoom = 1f;
    [Tooltip("Offset vertical/horizontal para centralizar a câmera perfeitamente no Boss durante a apresentação.")]
    public Vector2 cameraOffset = new Vector2(0f, 1.5f);

    public override void BindActors()
    {
        main = GameObject.Find("Player").GetComponent<Actor>();
        enemy = Boss.GetComponent<Actor>();
    }

    public override IEnumerator Play()
    {
        // Calcula o centro visual do Boss (ao invés do pivô no chão/pés)
        Vector3 bossCenter = enemy.transform.position + (Vector3)cameraOffset;
        SpriteRenderer sr = enemy.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.bounds.size.y > 0.5f)
        {
            bossCenter = sr.bounds.center;
        }

        Vector3 bossCamOffset = new Vector3(bossCenter.x - enemy.transform.position.x, bossCenter.y - enemy.transform.position.y, -10f);

        parent.camManager.Zoom(battleCameraZoom, 1f);
        yield return parent.camManager.Move(bossCenter, 2f).ToYieldInstruction();
        parent.camManager.SetTarget(enemy.transform, bossCamOffset);
        parent.camManager.Zoom(bossZoom, 1f);
        parent.ShowTitle(enemy.actorName, BossSubtitle);
        yield return parent.camManager.Shake(0.2f, 2f).ToYieldInstruction();
        yield return new WaitForSeconds(1f);
        parent.camManager.Zoom(battleCameraZoom, 1f);

        while (enemy.moving || main.moving) { yield return null; }
        parent.camManager.SetTarget(main.transform, new Vector3(0f, 0f, -10f));

        if (parent != null && parent.gameStateManager != null)
        {
            parent.gameStateManager.SetState(GameState.Playing);
        }
        else if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.SetState(GameState.Playing);
        }

        Destroy(gameObject);
    }
}
