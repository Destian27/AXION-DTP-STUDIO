using UnityEngine;

// Versi Unity 2020: GUITexture diganti SpriteRenderer pada kamera SUICamera.
public class SUISprite : SUIWidget
{
	private string mTextureFile;

	private string mLocalizedTextureFile;

	private Texture2D mTex;

	private Sprite mSprite;

	private GameObject mSpriteObject;

	private SpriteRenderer mRenderer;

	private float mAlpha = 1f;

	private Vector2 mScale = new Vector2(1f, 1f);

	private Vector2 mHotspot = new Vector2(0.5f, 0.5f);

	private bool mAutoScaleKeepAspectRatio = true;

	private float mHDScaleRatio = 1f;

	public string texture
	{
		get
		{
			return mTextureFile;
		}
		set
		{
			if (mTextureFile != value)
			{
				mTextureFile = value;
				mLocalizedTextureFile = Singleton<Localizer>.instance.LocalizeSpritePath(mTextureFile);
				reloadTexture();
			}
		}
	}

	public override float alpha
	{
		get
		{
			return mAlpha;
		}
		set
		{
			if (mAlpha != value)
			{
				mAlpha = value;
				if (mRenderer != null)
				{
					mRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(value));
				}
			}
		}
	}

	public float width
	{
		get
		{
			return (mTex != null) ? mTex.width : 0f;
		}
	}

	public float height
	{
		get
		{
			return (mTex != null) ? mTex.height : 0f;
		}
	}

	public override Vector2 scale
	{
		get
		{
			return mScale;
		}
		set
		{
			if (mScale != value)
			{
				mScale = value;
				updateScale();
			}
		}
	}

	public bool autoscaleKeepAspectRatio
	{
		get
		{
			return mAutoScaleKeepAspectRatio;
		}
		set
		{
			mAutoScaleKeepAspectRatio = value;
			updateScale();
		}
	}

	public Vector2 hotspot
	{
		get
		{
			return mHotspot;
		}
		set
		{
			mHotspot = value;
			updateScale();
		}
	}

	public Vector2 hotspotPixels
	{
		get
		{
			return new Vector2(mHotspot.x * (width - 1f), mHotspot.y * (height - 1f));
		}
		set
		{
			hotspot = new Vector2(value.x / (width - 1f), value.y / (height - 1f));
		}
	}

	public override Rect area
	{
		get
		{
			if (mRenderer == null || mTex == null)
			{
				return new Rect(position.x, position.y, 0f, 0f);
			}
			Bounds bounds = mRenderer.bounds;
			Rect r = new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
			return SUIUtils.unityToUser(r);
		}
	}

	public SUISprite()
	{
	}

	public SUISprite(string textureFile)
	{
		texture = textureFile;
	}

	public SUISprite(Color c, int width, int height)
	{
		Texture2D texture2D = new Texture2D(width, height);
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				texture2D.SetPixel(j, i, c);
			}
		}
		texture2D.Apply();
		setTexture(texture2D);
	}

	public override void Destroy()
	{
		if (mSprite != null)
		{
			Object.Destroy(mSprite);
		}
		mSprite = null;
		mRenderer = null;
		mSpriteObject = null;
		base.Destroy();
	}

	public void SetDynamicTextureColor(Color c)
	{
		if (mTex == null)
		{
			return;
		}
		for (int i = 0; (float)i < height; i++)
		{
			for (int j = 0; (float)j < width; j++)
			{
				mTex.SetPixel(j, i, c);
			}
		}
		mTex.Apply();
	}

	public override void EditorRenderOnGUI()
	{
		if (mTex == null)
		{
			return;
		}
		Color color = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, alpha);
		GUI.DrawTexture(area, mTex);
		GUI.color = color;
	}

	protected override void updatePosition()
	{
		base.updatePosition();
		if (mRenderer != null)
		{
			mRenderer.sortingOrder = sortingOrder;
		}
	}

	private void reloadTexture()
	{
		Texture2D texture2D = null;
		if (SUIScreen.isDevice_iPad3)
		{
			texture2D = (Texture2D)Resources.Load(mLocalizedTextureFile + "_ipad3", typeof(Texture2D));
			if (texture2D != null)
			{
				mHDScaleRatio = 0.5f;
			}
		}
		if (texture2D == null)
		{
			texture2D = (Texture2D)Resources.Load(mLocalizedTextureFile, typeof(Texture2D));
			if (texture2D == null)
			{
				Debug.Log("SUISprite -> Could not load texture: " + mTextureFile);
				return;
			}
			mHDScaleRatio = 1f;
		}
		setTexture(texture2D);
	}

	private void setTexture(Texture2D tex)
	{
		mTex = tex;
		if (mSpriteObject == null)
		{
			mSpriteObject = new GameObject("SUISpriteRenderer");
			mSpriteObject.layer = mGameObject.layer;
			mSpriteObject.transform.SetParent(mGameObject.transform, false);
			mRenderer = mSpriteObject.AddComponent<SpriteRenderer>();
		}
		if (mSprite != null)
		{
			Object.Destroy(mSprite);
		}
		mSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), Vector2.zero, 1f, 0u, SpriteMeshType.FullRect);
		mRenderer.sprite = mSprite;
		mRenderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01(mAlpha));
		mRenderer.sortingOrder = sortingOrder;
		updateScale();
	}

	private void updateScale()
	{
		if (mTex == null || mSpriteObject == null)
		{
			return;
		}
		AutoScaler autoScaler = WeakGlobalInstance<SUIScreen>.instance.autoScaler;
		Vector2 vector = ((!mAutoScaleKeepAspectRatio) ? new Vector2(autoScaler.toDeviceX((float)mTex.width * scale.x * mHDScaleRatio), autoScaler.toDeviceY((float)mTex.height * scale.y * mHDScaleRatio)) : new Vector2(autoScaler.toDevice((float)mTex.width * scale.x * mHDScaleRatio), autoScaler.toDevice((float)mTex.height * scale.y * mHDScaleRatio)));
		Transform transform = mSpriteObject.transform;
		transform.localPosition = new Vector3(0f - vector.x * mHotspot.x, 0f - vector.y * (1f - mHotspot.y), 0f);
		transform.localScale = new Vector3(vector.x / (float)mTex.width, vector.y / (float)mTex.height, 1f);
	}
}
