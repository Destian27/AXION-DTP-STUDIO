Cara memberi project ke workflow Android Ultimate:

1. Link Google Drive (paling mudah): pada Run workflow, isi drive_url dengan link folder atau file ZIP
   (dibagikan sebagai "Siapa saja yang memiliki link"). Diunduh dan diekstrak otomatis.
2. ZIP di repository: simpan ZIP di folder ini, commit, lalu isi project_zip, contoh: input/project-android.zip.
3. Folder di repository: kosongkan drive_url dan project_zip; workflow membangun project_dir (default: .).

Jangan memasukkan file JKS, password, token, atau secret ke ZIP/repository/Drive.
