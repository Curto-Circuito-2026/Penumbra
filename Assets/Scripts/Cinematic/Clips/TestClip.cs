using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestClip : ICinematicClip
{

    Actor main;
    Actor enemy;

    public override void BindActors()
    {
        main = GameObject.Find("Naia").GetComponent<Actor>();
        enemy = GameObject.Find("Cuca").GetComponent<Actor>();
    }

    public override IEnumerator Play()
    {
        yield return parent.camManager.Move(enemy.body.position, 2f).ToYieldInstruction();
        parent.camManager.SetTarget(enemy.transform);
        parent.camManager.Zoom(1f, 1f);
        parent.ShowTitle(enemy.actorName, "boladona");
        yield return parent.camManager.Shake(0.2f, 2f).ToYieldInstruction();
        yield return new WaitForSeconds(1f);
        parent.camManager.Zoom(5f, 1f);

        Vector2 pos1 = new Vector2(x: enemy.body.position.x - 10, y: enemy.body.position.y);
        MoveTarget move1 = new MoveTarget() { position = pos1, speed = 2f };

        Vector2 pos2 = new Vector2(x: enemy.body.position.x - 10, y: enemy.body.position.y + 10);
        MoveTarget move2 = new MoveTarget() { position = pos2, speed = 2f };

        List<MoveTarget> moveList = new List<MoveTarget> { move1, move2 };
        enemy.MoveSequence(moveList);

        while(enemy.moving || main.moving){yield return null;}
        parent.camManager.SetTarget(main.transform);

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
