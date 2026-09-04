using UnityEngine;

[ExecuteAlways]
public class MaterialProperties : MonoBehaviour
{
    private SpriteRenderer sprite;
    private MaterialPropertyBlock _propBlock;
    private int _spriteSizeID;

    private void Awake()
    {
        Initialize();
    }

    private void OnEnable()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
        if (_spriteSizeID == 0) _spriteSizeID = Shader.PropertyToID("_SpriteSize");
    }

    private void LateUpdate()
    {
        if (sprite == null) sprite = GetComponent<SpriteRenderer>();
        if (sprite == null || sprite.sprite == null) return;
        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
        if (_spriteSizeID == 0) _spriteSizeID = Shader.PropertyToID("_SpriteSize");

        sprite.GetPropertyBlock(_propBlock);
        _propBlock.SetVector(_spriteSizeID, new Vector2(sprite.sprite.rect.width, sprite.sprite.rect.height));
        sprite.SetPropertyBlock(_propBlock);
    }
}
