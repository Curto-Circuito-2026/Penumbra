using TMPro;
using UnityEngine;

public class WavyText : MonoBehaviour
{
    private TMP_Text textComponent;

    public float speed = 7f;
    public float frequency = 0.5f;
    public float height = 7f;

    public Color sparkleColor = Color.yellow;
    public float sparkleSpeed = 12f;
    [Range(0f, 1f)] public float sparkleChance = 0.4f;

    void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    void Update()
    {
        textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = textComponent.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];

            if (!charInfo.isVisible) continue;

            int materialIndex = charInfo.materialReferenceIndex;

            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            Color32[] colors = textInfo.meshInfo[materialIndex].colors32;

            float rawWave = Mathf.Sin(Time.time * speed + i * frequency) * height;
            Vector3 offset = new Vector3(0, rawWave, 0);

            int vertexIndex = charInfo.vertexIndex;

            for (int j = 0; j <= 4; j++){vertices[vertexIndex + j] += offset; }


            float noise = Mathf.PerlinNoise(Time.time * sparkleSpeed, i * 10f);

            if (noise > (1f - sparkleChance))
            {
                for (int j = 0; j <= 4; j++) { colors[vertexIndex + j] = sparkleColor; }
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
            textInfo.meshInfo[i].mesh.colors32 = textInfo.meshInfo[i].colors32;
            textComponent.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
