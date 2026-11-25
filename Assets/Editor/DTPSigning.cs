using UnityEditor;

// Mengisi password keystore otomatis (Unity 2017.4 tidak menyimpan password di ProjectSettings),
// supaya build di Unity Cloud langsung bertanda tangan tetap dan APK bisa diinstal/ditimpa.
[InitializeOnLoad]
public static class DTPSigning
{
    static DTPSigning()
    {
        PlayerSettings.Android.keystoreName = "Keystore/dtp.keystore";
        PlayerSettings.Android.keystorePass = "dtp123456";
        PlayerSettings.Android.keyaliasName = "dtpkey";
        PlayerSettings.Android.keyaliasPass = "dtp123456";
    }
}
