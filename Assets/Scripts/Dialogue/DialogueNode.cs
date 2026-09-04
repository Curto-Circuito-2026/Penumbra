using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Scripting;

[Preserve]
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

    [Header("Audio / Dublagem")]
    [Tooltip("Áudio de voz/dublagem correspondente a esta fala (opcional).")]
    [SerializeField] private AudioClip voiceClip;

    // Propriedades de acesso público
    public string SpeakerName => speakerName;
    public Sprite SpeakerPortrait => speakerPortrait;
    public string DialogueText => dialogueText;
    public DialogueNode NextNode => nextNode;
    public AudioClip VoiceClip => voiceClip;

    /// <summary>
    /// Inicializa o nó de diálogo em tempo de execução sem uso de Reflection (Compatível com WebGL / IL2CPP).
    /// </summary>
    public void InitializeRuntime(string speaker, string text, DialogueNode next = null, Sprite portrait = null, AudioClip voice = null)
    {
        this.speakerName = speaker;
        this.dialogueText = text;
        this.nextNode = next;
        this.speakerPortrait = portrait;
        this.voiceClip = voice;
    }
}
