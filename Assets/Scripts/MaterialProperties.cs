using UnityEngine;

public class MaterialProperties : MonoBehaviour
{

    SpriteRenderer sprite;
    private MaterialPropertyBlock _propBlock;

    private int _spriteSizeID;
    void Start()
    {
        
    }

    private void Awake()
    {
        sprite = GetComponent<SpriteRenderer>();
        _propBlock = new MaterialPropertyBlock();
        _spriteSizeID = Shader.PropertyToID("_SpriteSize");
    }

    void LateUpdate()
    {
        if (sprite.sprite == null) return;
        sprite.GetPropertyBlock(_propBlock);
        _propBlock.SetVector(_spriteSizeID, new Vector2(sprite.sprite.rect.width, sprite.sprite.rect.height));
        sprite.SetPropertyBlock(_propBlock);
    }
}
