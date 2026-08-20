using System.Collections;
using System.Collections.Generic;
using Unity.AI.Assistant.Agents;
using UnityEngine;

public class BossTrigger : ICinematicClip
{
    Actor main;
    Actor enemy;

    public GameObject Boss;
    public string BossSubtitle;
    public float battleCameraZoom = 5f;
    public float bossZoom = 1f;

    public override void BindActors()
    {
        main = GameObject.Find("Player").GetComponent<Actor>();
        enemy = Boss.GetComponent<Actor>();
    }

    public override IEnumerator Play()
    {
        parent.camManager.Zoom(battleCameraZoom, 1f);
        yield return parent.camManager.Move(enemy.body.position, 2f).ToYieldInstruction();
        parent.camManager.SetTarget(enemy.transform);
        parent.camManager.Zoom(bossZoom, 1f);
        parent.ShowTitle(enemy.actorName, BossSubtitle);
        yield return parent.camManager.Shake(0.2f, 2f).ToYieldInstruction();
        yield return new WaitForSeconds(1f);
        parent.camManager.Zoom(battleCameraZoom, 1f);

        while (enemy.moving || main.moving) { yield return null; }
        parent.camManager.SetTarget(main.transform);
        parent.gameStateManager.SetState(GameState.Playing);
        Destroy(gameObject);

    }
}
