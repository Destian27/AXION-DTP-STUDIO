// supabase/functions/maintenance-admin/index.ts
// Edge Function: kontrol Maintenance Mode — Samurai vs Zombie DTP 2.
// Menggantikan modul TS panel admin (maintenance-admin.ts): semua logika sekarang di server.
//
// Deploy (Cloud Shell):
//   supabase functions deploy maintenance-admin --project-ref zffctknzknqaxdofptnd --no-verify-jwt
//
// Panggil: POST https://<ref>.supabase.co/functions/v1/maintenance-admin
//   Header : apikey: <anon key>
//            Authorization: Bearer <access_token user admin>   (wajib untuk aksi admin)
//   Body   : { "action": "...", ... }
//
// Aksi:
//   status   (publik)  -> status yang juga dibaca game
//   whoami   (login)   -> { is_admin: boolean }
//   config   (admin)   -> baris mentah tabel game_maintenance
//   start    (admin)   -> { durationMinutes?, endsAt?, startsAt?, title?, message?, blockInGame? }
//   end      (admin)   -> akhiri sekarang
//   extend   (admin)   -> { minutes }
//
// Keamanan: hak admin TIDAK dicek di sini saja — fungsi RPC admin_* di maintenance.sql
// memeriksa is_admin() memakai JWT pemanggil, jadi tetap aman walau function ini bocor.

import { createClient } from 'npm:@supabase/supabase-js@2'

const SUPABASE_URL = Deno.env.get('SUPABASE_URL')!
const SUPABASE_ANON_KEY = Deno.env.get('SUPABASE_ANON_KEY')!

const CORS: Record<string, string> = {
  'Access-Control-Allow-Origin': '*',
  'Access-Control-Allow-Headers': 'authorization, x-client-info, apikey, content-type',
  'Access-Control-Allow-Methods': 'POST, OPTIONS',
}

function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { ...CORS, 'Content-Type': 'application/json' },
  })
}

class HttpError extends Error {
  constructor(public status: number, message: string) {
    super(message)
  }
}

function toIso(value: unknown, field: string): string | null {
  if (value == null || value === '') return null
  const d = new Date(value as string)
  if (Number.isNaN(d.getTime())) throw new HttpError(400, `${field} bukan tanggal yang valid`)
  return d.toISOString()
}

function toPositiveInt(value: unknown, field: string): number {
  const n = Number(value)
  if (!Number.isInteger(n) || n <= 0) throw new HttpError(400, `${field} harus bilangan bulat > 0`)
  return n
}

Deno.serve(async (req: Request): Promise<Response> => {
  if (req.method === 'OPTIONS') return new Response('ok', { headers: CORS })
  if (req.method !== 'POST') return json({ error: 'Gunakan POST' }, 405)

  try {
    let body: Record<string, unknown> = {}
    try {
      body = await req.json()
    } catch {
      throw new HttpError(400, 'Body harus JSON')
    }
    const action = String(body.action ?? '')

    // Client memakai JWT pemanggil -> auth.uid() di RPC = user yang login.
    const authHeader = req.headers.get('Authorization') ?? `Bearer ${SUPABASE_ANON_KEY}`
    const supabase = createClient(SUPABASE_URL, SUPABASE_ANON_KEY, {
      global: { headers: { Authorization: authHeader } },
      auth: { persistSession: false, autoRefreshToken: false },
    })

    const rpc = async (fn: string, args: Record<string, unknown> = {}) => {
      const { data, error } = await supabase.rpc(fn, args)
      if (error) {
        const forbidden = error.code === '42501' || /forbidden/i.test(error.message)
        throw new HttpError(forbidden ? 403 : 400, error.message)
      }
      return data
    }

    // --- publik ---
    if (action === 'status') {
      return json({ ok: true, data: await rpc('get_maintenance_status') })
    }

    // --- butuh login ---
    const { data: userData, error: userError } = await supabase.auth.getUser()
    if (userError || !userData?.user) throw new HttpError(401, 'Harus login sebagai admin')

    const isAdmin = (await rpc('is_admin')) === true
    if (action === 'whoami') return json({ ok: true, data: { is_admin: isAdmin } })
    if (!isAdmin) throw new HttpError(403, 'Akun ini bukan admin.')

    switch (action) {
      case 'config': {
        const { data, error } = await supabase.from('game_maintenance').select('*').eq('id', 1).single()
        if (error) throw new HttpError(400, error.message)
        return json({ ok: true, data })
      }

      case 'start': {
        const durationMinutes =
          body.durationMinutes == null ? null : toPositiveInt(body.durationMinutes, 'durationMinutes')
        const endsAt = durationMinutes == null ? toIso(body.endsAt, 'endsAt') : null
        const data = await rpc('admin_set_maintenance', {
          p_active: true,
          p_title: (body.title as string | undefined) ?? null,
          p_message: (body.message as string | undefined) ?? null,
          p_duration_minutes: durationMinutes,
          p_ends_at: endsAt,
          p_starts_at: toIso(body.startsAt, 'startsAt'),
          p_block_ingame: typeof body.blockInGame === 'boolean' ? body.blockInGame : null,
        })
        return json({ ok: true, data })
      }

      case 'end':
        return json({ ok: true, data: await rpc('admin_end_maintenance') })

      case 'extend':
        return json({
          ok: true,
          data: await rpc('admin_extend_maintenance', { p_minutes: toPositiveInt(body.minutes, 'minutes') }),
        })

      default:
        throw new HttpError(400, `Aksi tidak dikenal: "${action}"`)
    }
  } catch (e) {
    if (e instanceof HttpError) return json({ ok: false, error: e.message }, e.status)
    console.error(e)
    return json({ ok: false, error: 'Kesalahan server' }, 500)
  }
})
