# maintenance-admin (Edge Function)

1. Jalankan `migrations/20261126000000_maintenance.sql` di Supabase → SQL Editor (sekali, aman diulang).
2. Buat user admin di Authentication → Users, lalu jalankan bagian "JADIKAN AKUN KAMU ADMIN" di SQL.
3. Deploy dari Cloud Shell:
   ```
   supabase functions deploy maintenance-admin --project-ref zffctknzknqaxdofptnd --no-verify-jwt
   ```

Contoh dari panel admin (setelah `supabase.auth.signInWithPassword`):
```ts
const { data } = await supabase.functions.invoke('maintenance-admin', {
  body: { action: 'start', durationMinutes: 30, title: 'Update 2.2', message: 'Lagi nambah senjata baru!' },
})
// aksi lain: status | whoami | config | end | extend {minutes}
```
