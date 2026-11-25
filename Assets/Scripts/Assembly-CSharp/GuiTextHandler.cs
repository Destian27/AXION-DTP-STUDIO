using UnityEngine;

// GUIText sudah dihapus dari Unity. Teks fallback ini sekarang memakai TextMesh opsional;
// kalau komponen TextMesh tidak ada, GluiText tetap tampil normal dan handler ini tidak melakukan apa-apa.
public class GuiTextHandler : MonoBehaviour
{
	public GluiText TextObject;

	public float MaxYPos = 1f;

	public float MinYPos;

	private void Start()
	{
		if (TextObject == null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		enabled = false;
	}
}
