using UnityEngine;

// Pengganti GUILayer/GUITexture/GUIText (dihapus Unity 2019.3).
// Kamera ortografis khusus layer "SamuraiUI": 1 unit dunia = 1 piksel layar.
public static class SUICamera
{
	private const string kLayerName = "SamuraiUI";

	private static Camera sCamera;

	private static int sLastFrame = -1;

	public static int layer
	{
		get
		{
			int num = LayerMask.NameToLayer(kLayerName);
			return (num >= 0) ? num : 31;
		}
	}

	public static Camera camera
	{
		get
		{
			Ensure();
			return sCamera;
		}
	}

	public static void Ensure()
	{
		int num = layer;
		if (sCamera == null)
		{
			GameObject gameObject = new GameObject("SUICamera");
			gameObject.layer = num;
			sCamera = gameObject.AddComponent<Camera>();
			sCamera.clearFlags = CameraClearFlags.Depth;
			sCamera.orthographic = true;
			sCamera.nearClipPlane = 0.1f;
			sCamera.farClipPlane = 200f;
			sCamera.depth = 1000f;
			sCamera.cullingMask = 1 << num;
			sCamera.allowHDR = false;
			sCamera.allowMSAA = false;
			sCamera.useOcclusionCulling = false;
			sLastFrame = -1;
		}
		if (sLastFrame == Time.frameCount)
		{
			return;
		}
		sLastFrame = Time.frameCount;
		sCamera.orthographicSize = (float)Screen.height / 2f;
		sCamera.transform.position = new Vector3((float)Screen.width / 2f, (float)Screen.height / 2f, -100f);
		sCamera.transform.rotation = Quaternion.identity;
		int num2 = ~(1 << num);
		Camera[] allCameras = Camera.allCameras;
		foreach (Camera camera in allCameras)
		{
			if (camera != sCamera && (camera.cullingMask & ~num2) != 0)
			{
				camera.cullingMask &= num2;
			}
		}
	}
}
