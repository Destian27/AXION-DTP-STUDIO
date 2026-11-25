using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mengisi otomatis pengaturan tanda tangan (keystore) Android setiap Unity dibuka,
/// supaya Build And Run / Build langsung jalan tanpa mengisi password manual.
/// Tanda tangan yang tetap membuat APK baru bisa menimpa APK lama
/// (menghindari "Aplikasi tidak terinstal" karena beda tanda tangan).
/// </summary>
[InitializeOnLoad]
public static class AndroidSigning
{
    public const string KeystorePath = "Keystore/dtp.keystore";
    public const string KeystorePass = "dtp123456";
    public const string KeyAlias = "dtpkey";
    public const string KeyAliasPass = "dtp123456";

    static AndroidSigning()
    {
        Apply();
    }

    [MenuItem("DTP/Terapkan Keystore Android")]
    public static void Apply()
    {
        if (!File.Exists(KeystorePath))
        {
            Debug.LogWarning("[AndroidSigning] Keystore tidak ditemukan: " + KeystorePath);
            return;
        }
        PlayerSettings.Android.keystoreName = KeystorePath;
        PlayerSettings.Android.keystorePass = KeystorePass;
        PlayerSettings.Android.keyaliasName = KeyAlias;
        PlayerSettings.Android.keyaliasPass = KeyAliasPass;
    }
}
