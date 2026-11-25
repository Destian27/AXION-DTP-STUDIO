using UnityEngine.Networking;

/// <summary>
/// Server baru: Supabase. Anon key memang publik (aman ditaruh di klien),
/// keamanan dijaga oleh RLS + RPC di maintenance.sql.
/// JANGAN pernah taruh service_role key di sini.
/// </summary>
public static class SupabaseConfig
{
	public const string RestUrl = "https://zffctknzknqaxdofptnd.supabase.co/rest/v1/";

	public const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InpmZmN0a256a25xYXhkb2ZwdG5kIiwicm9sZSI6ImFub24iLCJpYXQiOjE3OTU2MjA1NDMsImV4cCI6MjExMTE5NjU0M30.KgNPfKmzZ5155MTvTLZmT4pUQcUUZ69g-PDsEDSkVv0";

	public static void ApplyHeaders(UnityWebRequest request)
	{
		request.SetRequestHeader("apikey", AnonKey);
		request.SetRequestHeader("Authorization", "Bearer " + AnonKey);
		request.SetRequestHeader("Content-Type", "application/json");
		request.SetRequestHeader("Accept", "application/json");
	}
}
