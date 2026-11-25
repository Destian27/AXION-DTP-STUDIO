using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Gerbang maintenance. Tidak perlu dipasang di scene: otomatis hidup saat game start.
/// - Cek status ke Supabase (RPC get_maintenance_status) tiap beberapa detik.
/// - Saat maintenance aktif: tampil layar penuh + hitung mundur, input Glu diblok, game di-pause.
/// - Saat selesai (admin mengakhiri / waktu habis): layar hilang sendiri, game lanjut.
/// Hitung mundur memakai jam SERVER (remaining_seconds), bukan jam HP.
/// </summary>
public class MaintenanceGate : MonoBehaviour
{
	[Serializable]
	private class StatusDto
	{
		public bool active;
		public string title;
		public string message;
		public bool has_end;
		public int remaining_seconds;
		public bool block_ingame;
	}

	private const float NormalPollSeconds = 3f;   // jeda cek normal
	private const float FastPollSeconds = 1f;     // jeda cek saat hitung mundur sudah 0 / hampir 0
	private const int RequestTimeoutSeconds = 5;

	private static MaintenanceGate instance;

	private StatusDto status;
	private float endRealtime;          // Time.realtimeSinceStartup saat maintenance berakhir (jam lokal, disinkron tiap poll)
	private bool enteredSession;        // true setelah cek pertama bilang "tidak maintenance"
	private bool blocking;
	private float prevTimeScale = 1f;
	private bool prevInputEnabled = true;

	private GameObject root;
	private Text titleText;
	private Text messageText;
	private Text countdownText;
	private Text footerText;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Bootstrap()
	{
		if (instance != null)
		{
			return;
		}
		GameObject go = new GameObject("MaintenanceGate");
		DontDestroyOnLoad(go);
		instance = go.AddComponent<MaintenanceGate>();
	}

	private void Awake()
	{
		BuildOverlay();
		SetOverlayVisible(false);
	}

	private void OnEnable()
	{
		StartCoroutine(PollLoop());
	}

	private IEnumerator PollLoop()
	{
		while (true)
		{
			yield return Fetch();

			bool nearEnd = blocking && status != null && status.has_end && (endRealtime - Time.realtimeSinceStartup) <= 3f;
			yield return new WaitForSecondsRealtime(nearEnd ? FastPollSeconds : NormalPollSeconds);
		}
	}

	private IEnumerator Fetch()
	{
		string url = SupabaseConfig.RestUrl + "rpc/get_maintenance_status";
		using (UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
		{
			req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
			req.downloadHandler = new DownloadHandlerBuffer();
			req.timeout = RequestTimeoutSeconds;
			SupabaseConfig.ApplyHeaders(req);

			yield return req.SendWebRequest();

			if (req.result != UnityWebRequest.Result.Success)
			{
				// Gagal konek: pertahankan status terakhir (fail-open kalau belum pernah dapat status).
				yield break;
			}

			string json = req.downloadHandler.text;
			if (string.IsNullOrEmpty(json) || json.Trim() == "null")
			{
				ApplyStatus(null);
				yield break;
			}

			StatusDto dto = null;
			try
			{
				dto = JsonUtility.FromJson<StatusDto>(json);
			}
			catch (Exception)
			{
				// JSON rusak: abaikan, coba lagi di poll berikutnya
			}
			if (dto != null)
			{
				ApplyStatus(dto);
			}
		}
	}

	private void ApplyStatus(StatusDto dto)
	{
		status = dto;
		bool active = dto != null && dto.active;

		if (!active)
		{
			enteredSession = true;
		}

		if (active && dto.has_end)
		{
			endRealtime = Time.realtimeSinceStartup + Mathf.Max(0, dto.remaining_seconds);
		}

		bool shouldBlock = active && (dto.block_ingame || !enteredSession);
		if (shouldBlock && !blocking)
		{
			BeginBlock();
		}
		else if (!shouldBlock && blocking)
		{
			EndBlock();
		}

		if (blocking)
		{
			RefreshTexts();
		}
	}

	private void BeginBlock()
	{
		blocking = true;
		prevTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
		Time.timeScale = 0f;
		if (InputManager.Exists)
		{
			prevInputEnabled = InputManager.Instance.InputEnabled;
			InputManager.Instance.InputEnabled = false;
		}
		SetOverlayVisible(true);
	}

	private void EndBlock()
	{
		blocking = false;
		Time.timeScale = prevTimeScale;
		if (InputManager.Exists)
		{
			InputManager.Instance.InputEnabled = prevInputEnabled;
		}
		SetOverlayVisible(false);
	}

	private void Update()
	{
		if (!blocking)
		{
			return;
		}

		// InputManager Glu bisa baru dibuat setelah blokir dimulai: paksa tetap mati selama maintenance.
		if (InputManager.Exists && InputManager.Instance.InputEnabled)
		{
			InputManager.Instance.InputEnabled = false;
		}

		if (Time.timeScale != 0f)
		{
			Time.timeScale = 0f;
		}

		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Application.Quit();
		}

		RefreshTexts();
	}

	private void RefreshTexts()
	{
		if (status == null || titleText == null)
		{
			return;
		}

		titleText.text = string.IsNullOrEmpty(status.title) ? "Maintenance" : status.title;
		messageText.text = status.message ?? string.Empty;

		if (status.has_end)
		{
			int remaining = Mathf.Max(0, Mathf.CeilToInt(endRealtime - Time.realtimeSinceStartup));
			if (remaining > 0)
			{
				TimeSpan t = TimeSpan.FromSeconds(remaining);
				countdownText.text = string.Format("{0:00}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds);
				footerText.text = "Maintenance selesai dalam hitungan di atas.\nGame otomatis terbuka saat selesai.";
			}
			else
			{
				countdownText.text = "00:00:00";
				footerText.text = "Memeriksa status...";
			}
		}
		else
		{
			countdownText.text = "--:--:--";
			footerText.text = "Waktu selesai belum ditentukan.\nGame otomatis terbuka saat maintenance selesai.";
		}
	}

	// ------------------------------------------------------------------
	// UI (dibuat lewat kode, tanpa prefab / scene)
	// ------------------------------------------------------------------

	private void BuildOverlay()
	{
		root = new GameObject("MaintenanceCanvas");
		root.transform.SetParent(transform, false);

		Canvas canvas = root.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = short.MaxValue;

		CanvasScaler scaler = root.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
		scaler.matchWidthOrHeight = 0.5f;

		GameObject bg = new GameObject("Background");
		bg.transform.SetParent(root.transform, false);
		Image bgImage = bg.AddComponent<Image>();
		bgImage.color = new Color(0.04f, 0.04f, 0.07f, 0.97f);
		Stretch(bg.GetComponent<RectTransform>());

		Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

		titleText = CreateText("Title", font, 72, FontStyle.Bold, new Color(1f, 0.82f, 0.25f), 0.70f, 0.88f);
		messageText = CreateText("Message", font, 40, FontStyle.Normal, Color.white, 0.50f, 0.70f);
		countdownText = CreateText("Countdown", font, 140, FontStyle.Bold, Color.white, 0.28f, 0.50f);
		footerText = CreateText("Footer", font, 32, FontStyle.Normal, new Color(0.7f, 0.7f, 0.75f), 0.08f, 0.26f);
	}

	private Text CreateText(string name, Font font, int size, FontStyle style, Color color, float yMin, float yMax)
	{
		GameObject go = new GameObject(name);
		go.transform.SetParent(root.transform, false);
		Text text = go.AddComponent<Text>();
		text.font = font;
		text.fontSize = size;
		text.fontStyle = style;
		text.color = color;
		text.alignment = TextAnchor.MiddleCenter;
		text.horizontalOverflow = HorizontalWrapMode.Wrap;
		text.verticalOverflow = VerticalWrapMode.Overflow;
		text.raycastTarget = false;
		RectTransform rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0.08f, yMin);
		rt.anchorMax = new Vector2(0.92f, yMax);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return text;
	}

	private static void Stretch(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	private void SetOverlayVisible(bool visible)
	{
		if (root != null)
		{
			root.SetActive(visible);
		}
	}
}
