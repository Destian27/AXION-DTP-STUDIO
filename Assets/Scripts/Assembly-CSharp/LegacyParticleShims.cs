using UnityEngine;

// Kompatibilitas: Unity 6 sudah menghapus komponen partikel lama (Legacy Particle System).
// Kode game lama masih memanggil tipe ini lewat GetComponentsInChildren<...>(). Karena prefab
// hasil konversi memakai ParticleSystem baru, komponen ini tidak pernah ada di scene,
// jadi array hasilnya kosong dan perilaku game tidak berubah. Efek asli tetap dikendalikan
// lewat ParticleSystem.
public class ParticleEmitter : MonoBehaviour
{
	public bool emit { get; set; }

	public int particleCount
	{
		get { return 0; }
	}
}

public class ParticleRenderer : MonoBehaviour
{
}
