using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Dialogue/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("Nó de diálogo inicial desta conversa.")]
    [SerializeField] private List<DialogueNode> startingNode;

    // Propriedade de acesso público
    public List<DialogueNode> StartingNode => startingNode;

    public DialogueNode getNode()
    {
        if (startingNode.Count > 1)
        {
            int randomIndex = Random.Range(0, startingNode.Count);

            DialogueNode node = startingNode[randomIndex];
            if(node == null)
            { node = startingNode[0];}
            return node;
        }
        else
        {
            return startingNode[0];
        }
    }
}
