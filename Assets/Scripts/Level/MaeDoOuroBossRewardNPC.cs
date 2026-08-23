using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum BossDefeatedType
{
    Boitata,
    Mapinguari,
    Matinta,
    Cuca
}

/// <summary>
/// Controla o surgimento e diálogo da Mãe do Ouro após a derrota de um Boss.
/// Aguarda a coleta de todas as estrelas forjadas, surge no local da morte do chefe
/// com efeitos visuais e áureos, apresenta um diálogo exclusivo para o chefe derrotado
/// e transporta a Naia de volta para a cena Hub (Céu) ao concluir a conversa.
/// </summary>
public class MaeDoOuroBossRewardNPC : MonoBehaviour
{
    [Header("Configuração do Chefe")]
    [SerializeField] private BossDefeatedType bossType;

    [Header("Sequências de Diálogo por Boss (Opcional - Criadas dinamicamente se nulas)")]
    [SerializeField] private DialogueSequence boitataDialogue;
    [SerializeField] private DialogueSequence mapinguariDialogue;
    [SerializeField] private DialogueSequence matintaDialogue;
    [SerializeField] private DialogueSequence cucaDialogue;

    [Header("Feedback Visual de Spawn")]
    [SerializeField] private Color goldFlashColor = new Color(1f, 0.88f, 0.2f, 1f);

    private DialogueTrigger dialogueTrigger;
    private SpriteRenderer spriteRenderer;
    private Collider2D npcCollider;
    private bool hasTriggeredReturn = false;

    private void Awake()
    {
        dialogueTrigger = GetComponent<DialogueTrigger>();
        if (dialogueTrigger == null) dialogueTrigger = gameObject.AddComponent<DialogueTrigger>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        npcCollider = GetComponent<Collider2D>();
    }

    /// <summary>
    /// Método estático principal invocado pelos Bosses ao morrerem.
    /// </summary>
    public static void SpawnAfterBoss(Vector3 position, BossDefeatedType defeatedBoss)
    {
        GameObject maePrefab = Resources.Load<GameObject>("NPC_MaeDoOuro") 
                               ?? Resources.Load<GameObject>("NPCs/NPC_MaeDoOuro")
                               ?? Resources.Load<GameObject>("Prefabs/NPCs/NPC_MaeDoOuro");

        GameObject maeObj;
        if (maePrefab != null)
        {
            maeObj = Instantiate(maePrefab, position, Quaternion.identity);
        }
        else
        {
            // Criação procedural caso o prefab não esteja na pasta Resources
            maeObj = new GameObject("NPC_MaeDoOuro_Reward");
            maeObj.transform.position = position;

            SpriteRenderer sr = maeObj.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("shop_maedoouro_0");
            sr.color = Color.white;
            sr.sortingOrder = 3;

            CircleCollider2D col = maeObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 1.2f;

            maeObj.AddComponent<DialogueTrigger>();
        }

        MaeDoOuroBossRewardNPC rewardComp = maeObj.GetComponent<MaeDoOuroBossRewardNPC>();
        if (rewardComp == null)
        {
            rewardComp = maeObj.AddComponent<MaeDoOuroBossRewardNPC>();
        }

        rewardComp.InitAndAppear(defeatedBoss);
    }

    public void InitAndAppear(BossDefeatedType defeatedBoss)
    {
        bossType = defeatedBoss;

        // Fade out suave da música de batalha do Boss para o silêncio
        if (AudioController.Instance != null)
        {
            AudioController.Instance.StopBGM(fadeDuration: 2.0f);
        }

        StartCoroutine(AppearSequenceRoutine());
    }

    private IEnumerator AppearSequenceRoutine()
    {
        // Começa invisível e com colisor desativado enquanto as estrelas voam
        transform.localScale = Vector3.zero;
        if (npcCollider != null) npcCollider.enabled = false;

        // Aguarda todas as estrelas serem coletadas (ou timeout de segurança de 3.5 segundos)
        float timer = 0f;
        float maxWait = 3.5f;

        // Espera mínima para as estrelas começarem a voar
        yield return new WaitForSeconds(0.8f);

        while (StarPickup.ActiveStarsCount > 0 && timer < maxWait)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(0.3f);

        // Efeito de Aparição (Impact Burst de Luz Dourada)
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, goldFlashColor, 2.8f);
            CombatVisualEffects.Instance.SpawnFloatingText(transform.position + Vector3.up * 1.5f, "✦ A Mãe do Ouro Surgiu! ✦", goldFlashColor, 4.5f);
        }

        // Animação suave de surgimento (Pop com bounce)
        Vector3 targetScale = Vector3.one;
        Tween.Scale(transform, targetScale, 0.6f, Ease.OutBack);

        if (npcCollider != null) npcCollider.enabled = true;

        // Configura o Diálogo exclusivo do Boss
        SetupDialogueForBoss(bossType);
    }

    private void SetupDialogueForBoss(BossDefeatedType boss)
    {
        DialogueSequence seq = GetDialogueSequenceForBoss(boss);
        if (dialogueTrigger != null)
        {
            dialogueTrigger.SetDialogueSequence(seq);
            dialogueTrigger.SetOnDialogueFinished(HandleDialogueFinished);
        }
    }

    private DialogueSequence GetDialogueSequenceForBoss(BossDefeatedType boss)
    {
        switch (boss)
        {
            case BossDefeatedType.Boitata:
                if (boitataDialogue != null) return boitataDialogue;
                return CreateRuntimeDialogue("Mãe do Ouro", new string[]
                {
                    "Ora ora, Naia! Você conseguiu apagar as chamas vorazes do Boitatá sem queimar essas matas inteiras!",
                    "Essas estrelas que você recuperou têm um brilho tão gostoso... Vamos continuar nossa caçada!",
                    "Venha, segure firme no meu manto dourado. Vou te guiar direto para o próximo desafio!"
                });

            case BossDefeatedType.Mapinguari:
                if (mapinguariDialogue != null) return mapinguariDialogue;
                return CreateRuntimeDialogue("Mãe do Ouro", new string[]
                {
                    "Que estrondo medonho fazia aquele grandalhão! Minhas orelhas douradas quase derreteram com aquele berro cavernoso!",
                    "Mas veja só, você arrancou as estrelas de dentro da fera com pura maestria! Nada mal para uma mortal corajosa.",
                    "Chega de poeira e ruínas por hoje. Vamos seguir em frente para a próxima terra!"
                });

            case BossDefeatedType.Matinta:
                if (matintaDialogue != null) return matintaDialogue;
                return CreateRuntimeDialogue("Mãe do Ouro", new string[]
                {
                    "Aquele assobio irritante finalmente parou! Eu já não aguentava mais aquela velha ranzinza assustando meus vagalumes no pântano.",
                    "Você não se perdeu no nevoeiro espesso e ainda trouxe as estrelas roubadas sãs e salvas!",
                    "Segure firme no meu manto. Hora de voar direto para o covil final da Cuca!"
                });

            case BossDefeatedType.Cuca:
                if (cucaDialogue != null) return cucaDialogue;
                return CreateRuntimeDialogue("Mãe do Ouro", new string[]
                {
                    "A grande feiticeira virou jacaré sem caldeirão! Quem diria que a poção sombria dela ia entornar desse jeito, hein?",
                    "Você resgatou todo o ouro celestial que havia caído do firmamento! O Céu resplandece em gratidão eterna.",
                    "Seu destino foi cumprido com glória, Naia! Vamos voltar triunfantes ao Céu para celebrar nossa grande vitória!"
                });
        }

        return CreateRuntimeDialogue("Mãe do Ouro", new string[]
        {
            "Você foi magnífica nesta batalha!",
            "Vamos continuar em frente!"
        });
    }

    private DialogueSequence CreateRuntimeDialogue(string speaker, string[] lines)
    {
        DialogueSequence seq = ScriptableObject.CreateInstance<DialogueSequence>();
        if (lines == null || lines.Length == 0) return seq;

        List<DialogueNode> nodes = new List<DialogueNode>();
        for (int i = 0; i < lines.Length; i++)
        {
            DialogueNode node = ScriptableObject.CreateInstance<DialogueNode>();
            var speakerProp = typeof(DialogueNode).GetField("speakerName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var textProp = typeof(DialogueNode).GetField("dialogueText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (speakerProp != null) speakerProp.SetValue(node, speaker);
            if (textProp != null) textProp.SetValue(node, lines[i]);

            nodes.Add(node);
        }

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            var nextProp = typeof(DialogueNode).GetField("nextNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (nextProp != null) nextProp.SetValue(nodes[i], nodes[i + 1]);
        }

        var startProp = typeof(DialogueSequence).GetField("startingNode", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (startProp != null) startProp.SetValue(seq, nodes[0]);

        return seq;
    }

    private void HandleDialogueFinished()
    {
        if (hasTriggeredReturn) return;
        hasTriggeredReturn = true;

        StartCoroutine(AdvanceSequence());
    }

    private IEnumerator AdvanceSequence()
    {
        // Efeito de Teleporte / Despedida
        if (CombatVisualEffects.Instance != null)
        {
            CombatVisualEffects.Instance.PlayImpactBurst(transform.position, goldFlashColor, 3f);
        }

        yield return new WaitForSeconds(0.4f);

        // Avança diretamente para a próxima fase da fila ou conclui a jornada
        RunManager runManager = RunManager.Instance ?? FindAnyObjectByType<RunManager>();
        if (runManager != null)
        {
            runManager.AdvanceToNextRegionOrFinish();
        }
        else
        {
            SceneController sceneController = FindAnyObjectByType<SceneController>();
            if (sceneController != null)
            {
                sceneController.LoadScene("Hub", TransitionType.CrossFade);
            }
            else
            {
                SceneManager.LoadScene("Hub");
            }
        }
    }
}
