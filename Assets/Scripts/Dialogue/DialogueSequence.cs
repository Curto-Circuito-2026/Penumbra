using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

[Preserve]
[CreateAssetMenu(fileName = "NewDialogueSequence", menuName = "Dialogue/Dialogue Sequence")]
public class DialogueSequence : ScriptableObject
{
    [Tooltip("Nó de diálogo inicial desta conversa.")]
    [SerializeField] private List<DialogueNode> startingNode = new List<DialogueNode>();

    // Propriedade de acesso público
    public List<DialogueNode> StartingNode => startingNode;

    /// <summary>
    /// Inicializa a sequência de diálogos em tempo de execução sem uso de Reflection (Compatível com WebGL / IL2CPP).
    /// </summary>
    public void InitializeRuntime(List<DialogueNode> nodes)
    {
        if (startingNode == null) startingNode = new List<DialogueNode>();
        startingNode.Clear();
        if (nodes != null)
        {
            startingNode.AddRange(nodes);
        }
    }

    public DialogueNode getNode()
    {
        if (startingNode != null && startingNode.Count > 1)
        {
            int randomIndex = Random.Range(0, startingNode.Count);

            DialogueNode node = startingNode[randomIndex];
            if(node == null)
            { node = startingNode[0];}
            return node;
        }
        else if (startingNode != null && startingNode.Count > 0)
        {
            return startingNode[0];
        }
        return null;
    }
}
