using UnityEngine;

// Pengganti struct Particle lama (UnityEngine.Particle, dihapus di Unity 2018+).
public struct Particle
{
	public Vector3 position;

	public Vector3 velocity;

	public float energy;

	public float startEnergy;

	public float size;

	public float rotation;

	public float angularVelocity;

	public Color color;
}

// Pengganti EllipsoidParticleEmitter + ParticleAnimator + ParticleRenderer lama.
// Pengaturan lama disimpan sebagai field, lalu dibuatkan ParticleSystem modern saat runtime.
public class ParticleEmitter : MonoBehaviour
{
	// ---- bekas EllipsoidParticleEmitter ----
	public bool emitOnStart = true;

	[SerializeField]
	private float m_minSize = 1f;

	[SerializeField]
	private float m_maxSize = 1f;

	public float minEnergy = 1f;

	public float maxEnergy = 1f;

	public float minEmission;

	public float maxEmission;

	public Vector3 worldVelocity;

	public Vector3 localVelocity;

	public Vector3 rndVelocity;

	public float emitterVelocityScale;

	public Vector3 tangentVelocity;

	public float angularVelocity;

	public float rndAngularVelocity;

	public bool rndRotation;

	public bool useWorldSpace;

	public bool oneShot;

	public Vector3 ellipsoid;

	public float minEmitterRange;

	// ---- bekas ParticleAnimator ----
	public bool doesAnimateColor;

	public Color color0 = Color.white;

	public Color color1 = Color.white;

	public Color color2 = Color.white;

	public Color color3 = Color.white;

	public Color color4 = Color.white;

	public Vector3 worldRotationAxis;

	public Vector3 localRotationAxis;

	public float sizeGrow;

	public Vector3 rndForce;

	public Vector3 force;

	public float damping = 1f;

	public bool autodestruct;

	// ---- bekas ParticleRenderer ----
	public bool rendererEnabled = true;

	public Material rendererMaterial;

	public int stretchMode;

	public float lengthScale = 1f;

	public float velocityScale;

	public float cameraVelocityScale;

	public float maxParticleSize = 0.5f;

	public int uvTilesX = 1;

	public int uvTilesY = 1;

	public float uvCycles = 1f;

	private ParticleSystem mSystem;

	private bool mBuilt;

	private bool mEmit = true;

	private ParticleSystem.Particle[] mBuffer = new ParticleSystem.Particle[0];

	public float minSize
	{
		get
		{
			return m_minSize;
		}
		set
		{
			m_minSize = value;
			applySize();
		}
	}

	public float maxSize
	{
		get
		{
			return m_maxSize;
		}
		set
		{
			m_maxSize = value;
			applySize();
		}
	}

	public bool emit
	{
		get
		{
			return mEmit;
		}
		set
		{
			mEmit = value;
			if (EnsureBuilt())
			{
				ParticleSystem.EmissionModule emission = mSystem.emission;
				emission.enabled = value;
				if (value && !mSystem.isPlaying)
				{
					mSystem.Play(true);
				}
			}
		}
	}

	public int particleCount
	{
		get
		{
			return EnsureBuilt() ? mSystem.particleCount : 0;
		}
	}

	public Particle[] particles
	{
		get
		{
			if (!EnsureBuilt())
			{
				return new Particle[0];
			}
			int num = Mathf.Max(mSystem.main.maxParticles, 1);
			if (mBuffer.Length < num)
			{
				mBuffer = new ParticleSystem.Particle[num];
			}
			int num2 = mSystem.GetParticles(mBuffer);
			Particle[] array = new Particle[num2];
			for (int i = 0; i < num2; i++)
			{
				ParticleSystem.Particle particle = mBuffer[i];
				array[i].position = particle.position;
				array[i].velocity = particle.velocity;
				array[i].energy = particle.remainingLifetime;
				array[i].startEnergy = particle.startLifetime;
				array[i].size = particle.GetCurrentSize(mSystem);
				array[i].rotation = particle.rotation * ((float)Mathf.PI / 180f);
				array[i].angularVelocity = particle.angularVelocity * ((float)Mathf.PI / 180f);
				array[i].color = particle.GetCurrentColor(mSystem);
			}
			return array;
		}
		set
		{
			if (!EnsureBuilt() || value == null)
			{
				return;
			}
			int num = Mathf.Max(mSystem.main.maxParticles, 1);
			if (mBuffer.Length < num)
			{
				mBuffer = new ParticleSystem.Particle[num];
			}
			int num2 = mSystem.GetParticles(mBuffer);
			int num3 = Mathf.Min(num2, value.Length);
			for (int i = 0; i < num3; i++)
			{
				mBuffer[i].position = value[i].position;
				mBuffer[i].velocity = value[i].velocity;
				mBuffer[i].remainingLifetime = value[i].energy;
				if (Mathf.Abs(value[i].size - mBuffer[i].GetCurrentSize(mSystem)) > 0.0001f)
				{
					mBuffer[i].startSize = value[i].size;
				}
				Color currentColor = mBuffer[i].GetCurrentColor(mSystem);
				Color color = value[i].color;
				if (Mathf.Abs(currentColor.r - color.r) + Mathf.Abs(currentColor.g - color.g) + Mathf.Abs(currentColor.b - color.b) + Mathf.Abs(currentColor.a - color.a) > 0.004f)
				{
					mBuffer[i].startColor = color;
				}
			}
			mSystem.SetParticles(mBuffer, num2);
		}
	}

	private void Awake()
	{
		EnsureBuilt();
	}

	private void OnEnable()
	{
		if (mBuilt && mSystem != null && !mSystem.isPlaying)
		{
			mSystem.Play(true);
		}
	}

	public void Simulate(float t)
	{
		if (EnsureBuilt())
		{
			mSystem.Simulate(t, true, false);
			mSystem.Play(true);
		}
	}

	public void ClearParticles()
	{
		if (EnsureBuilt())
		{
			mSystem.Clear(true);
		}
	}

	private void applySize()
	{
		if (mBuilt && mSystem != null)
		{
			ParticleSystem.MainModule main = mSystem.main;
			main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(m_minSize, m_maxSize), Mathf.Max(m_minSize, m_maxSize));
		}
	}

	private bool EnsureBuilt()
	{
		if (!Application.isPlaying)
		{
			return false;
		}
		if (mBuilt)
		{
			return mSystem != null;
		}
		mBuilt = true;
		mEmit = emitOnStart;
		mSystem = GetComponent<ParticleSystem>();
		if (mSystem == null)
		{
			mSystem = base.gameObject.AddComponent<ParticleSystem>();
		}
		mSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

		ParticleSystem.MainModule main = mSystem.main;
		main.playOnAwake = false;
		main.loop = !oneShot;
		main.duration = 5f;
		main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Min(minEnergy, maxEnergy), Mathf.Max(minEnergy, maxEnergy));
		main.startSpeed = 0f;
		main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Min(m_minSize, m_maxSize), Mathf.Max(m_minSize, m_maxSize));
		main.startRotation = (rndRotation ? new ParticleSystem.MinMaxCurve(0f, (float)Mathf.PI * 2f) : new ParticleSystem.MinMaxCurve(0f));
		main.startColor = Color.white;
		main.gravityModifier = 0f;
		main.maxParticles = 2000;
		main.simulationSpace = (useWorldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local);
		main.scalingMode = ParticleSystemScalingMode.Shape;
		if (autodestruct)
		{
			main.stopAction = ParticleSystemStopAction.Destroy;
		}

		ParticleSystem.EmissionModule emission = mSystem.emission;
		emission.enabled = emitOnStart;
		if (oneShot)
		{
			emission.rateOverTime = 0f;
			short num = (short)Mathf.Max(0, Mathf.RoundToInt(Mathf.Min(minEmission, maxEmission)));
			short num2 = (short)Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(minEmission, maxEmission)));
			emission.SetBursts(new ParticleSystem.Burst[1]
			{
				new ParticleSystem.Burst(0f, num, num2)
			});
		}
		else
		{
			emission.rateOverTime = new ParticleSystem.MinMaxCurve(Mathf.Min(minEmission, maxEmission), Mathf.Max(minEmission, maxEmission));
		}

		ParticleSystem.ShapeModule shape = mSystem.shape;
		bool flag = ellipsoid.sqrMagnitude > 0.0001f;
		shape.enabled = flag;
		if (flag)
		{
			shape.shapeType = ParticleSystemShapeType.Sphere;
			shape.radius = 1f;
			shape.scale = ellipsoid;
		}

		bool flag2 = localVelocity.sqrMagnitude > 0f || rndVelocity.sqrMagnitude > 0f;
		Vector3 vector = (flag2 ? localVelocity : worldVelocity);
		if (vector.sqrMagnitude > 0f || rndVelocity.sqrMagnitude > 0f)
		{
			ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = mSystem.velocityOverLifetime;
			velocityOverLifetime.enabled = true;
			velocityOverLifetime.space = (flag2 ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World);
			velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(vector.x - rndVelocity.x, vector.x + rndVelocity.x);
			velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(vector.y - rndVelocity.y, vector.y + rndVelocity.y);
			velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(vector.z - rndVelocity.z, vector.z + rndVelocity.z);
		}

		if (angularVelocity != 0f || rndAngularVelocity != 0f)
		{
			ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = mSystem.rotationOverLifetime;
			rotationOverLifetime.enabled = true;
			rotationOverLifetime.z = new ParticleSystem.MinMaxCurve((angularVelocity - rndAngularVelocity) * ((float)Mathf.PI / 180f), (angularVelocity + rndAngularVelocity) * ((float)Mathf.PI / 180f));
		}

		if (doesAnimateColor)
		{
			Color[] array = new Color[5] { color0, color1, color2, color3, color4 };
			GradientColorKey[] array2 = new GradientColorKey[5];
			GradientAlphaKey[] array3 = new GradientAlphaKey[5];
			for (int i = 0; i < 5; i++)
			{
				float time = (float)i / 4f;
				array2[i] = new GradientColorKey(new Color(array[i].r, array[i].g, array[i].b, 1f), time);
				array3[i] = new GradientAlphaKey(array[i].a, time);
			}
			Gradient gradient = new Gradient();
			gradient.SetKeys(array2, array3);
			ParticleSystem.ColorOverLifetimeModule colorOverLifetime = mSystem.colorOverLifetime;
			colorOverLifetime.enabled = true;
			colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
		}

		if (sizeGrow != 0f)
		{
			float num3 = Mathf.Max((m_minSize + m_maxSize) * 0.5f, 0.0001f);
			float num4 = (minEnergy + maxEnergy) * 0.5f;
			float num5 = Mathf.Max(0f, 1f + sizeGrow * num4 / num3);
			ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = mSystem.sizeOverLifetime;
			sizeOverLifetime.enabled = true;
			sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, num5));
		}

		if (force.sqrMagnitude > 0f || rndForce.sqrMagnitude > 0f)
		{
			ParticleSystem.ForceOverLifetimeModule forceOverLifetime = mSystem.forceOverLifetime;
			forceOverLifetime.enabled = true;
			forceOverLifetime.space = ParticleSystemSimulationSpace.World;
			forceOverLifetime.x = new ParticleSystem.MinMaxCurve(force.x - rndForce.x, force.x + rndForce.x);
			forceOverLifetime.y = new ParticleSystem.MinMaxCurve(force.y - rndForce.y, force.y + rndForce.y);
			forceOverLifetime.z = new ParticleSystem.MinMaxCurve(force.z - rndForce.z, force.z + rndForce.z);
		}

		if (damping < 1f)
		{
			ParticleSystem.LimitVelocityOverLifetimeModule limitVelocityOverLifetime = mSystem.limitVelocityOverLifetime;
			limitVelocityOverLifetime.enabled = true;
			limitVelocityOverLifetime.limit = 0f;
			limitVelocityOverLifetime.dampen = Mathf.Clamp01(1f - damping);
		}

		if (uvTilesX * uvTilesY > 1)
		{
			ParticleSystem.TextureSheetAnimationModule textureSheetAnimation = mSystem.textureSheetAnimation;
			textureSheetAnimation.enabled = true;
			textureSheetAnimation.numTilesX = Mathf.Max(1, uvTilesX);
			textureSheetAnimation.numTilesY = Mathf.Max(1, uvTilesY);
			textureSheetAnimation.animation = ParticleSystemAnimationType.WholeSheet;
			textureSheetAnimation.cycleCount = Mathf.Max(1, Mathf.RoundToInt(uvCycles));
			textureSheetAnimation.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
		}

		ParticleSystemRenderer component = mSystem.GetComponent<ParticleSystemRenderer>();
		if (component != null)
		{
			if (rendererMaterial != null)
			{
				component.sharedMaterial = rendererMaterial;
			}
			switch (stretchMode)
			{
			case 1:
				component.renderMode = ParticleSystemRenderMode.Stretch;
				component.lengthScale = lengthScale;
				component.velocityScale = velocityScale;
				component.cameraVelocityScale = cameraVelocityScale;
				break;
			case 4:
				component.renderMode = ParticleSystemRenderMode.VerticalBillboard;
				break;
			case 5:
				component.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
				break;
			default:
				component.renderMode = ParticleSystemRenderMode.Billboard;
				break;
			}
			component.maxParticleSize = Mathf.Clamp(maxParticleSize, 0.01f, 10f);
			component.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			component.receiveShadows = false;
			component.enabled = rendererEnabled;
		}

		mSystem.Play(true);
		return true;
	}
}
