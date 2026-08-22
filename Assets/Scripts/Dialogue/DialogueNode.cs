using UnityEngine;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "NewDialogueNode", menuName = "Dialogue/Dialogue Node")]
public class DialogueNode : ScriptableObject
{
    [Header("Speaker Info")]
    [Tooltip("Nome de quem fala.")]
    [SerializeField] private string speakerName;

    [Tooltip("Retrato opcional do personagem.")]
    [SerializeField] private Sprite speakerPortrait;

    [Header("Dialogue Content")]
    [Tooltip("O texto da fala.")]
    [TextArea(3, 10)]
    [SerializeField] private string dialogueText;

    [Tooltip("Referência para a próxima fala da conversa.")]
    [SerializeField] private DialogueNode nextNode;

    [Tooltip("Evento para rodar no começo desse nó")]
    [SerializeField] public GameEvent onStart;

    [Tooltip("Evento para rodar no final desse nó")]
    [SerializeField] public GameEvent onEnd;

    // Propriedades de acesso público
    public string SpeakerName => speakerName;
    public Sprite SpeakerPortrait => speakerPortrait;
    public string DialogueText => dialogueText;
    public DialogueNode NextNode => nextNode;
}
