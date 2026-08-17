using UnityEngine;

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

    // Propriedades de acesso público
    public string SpeakerName => speakerName;
    public Sprite SpeakerPortrait => speakerPortrait;
    public string DialogueText => dialogueText;
    public DialogueNode NextNode => nextNode;
}
