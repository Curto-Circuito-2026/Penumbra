using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Dialogue/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("Nó de diálogo inicial desta conversa.")]
    [SerializeField] private DialogueNode startingNode;

    // Propriedade de acesso público
    public DialogueNode StartingNode => startingNode;
}
