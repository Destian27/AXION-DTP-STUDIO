-- =====================================================================
-- MAINTENANCE MODE — Samurai vs Zombie DTP 2
-- Jalankan sekali di Supabase Dashboard → SQL Editor.
-- Aman dijalankan ulang (idempotent).
-- =====================================================================

-- 1) Tabel konfigurasi (selalu 1 baris, id = 1) -----------------------
create table if not exists public.game_maintenance (
  id            int primary key default 1 check (id = 1),
  is_active     boolean     not null default false,
  title         text        not null default 'Maintenance',
  message       text        not null default 'Game sedang dalam perawatan. Silakan kembali lagi nanti.',
  starts_at     timestamptz,                       -- null = mulai langsung
  ends_at       timestamptz,                       -- null = tanpa batas (manual dimatikan admin)
  block_ingame  boolean     not null default true, -- true = pemain yang sedang main ikut di-block
  updated_at    timestamptz not null default now(),
  updated_by    uuid references auth.users(id),
  constraint maintenance_range_ok check (ends_at is null or starts_at is null or ends_at > starts_at)
);

insert into public.game_maintenance (id) values (1) on conflict (id) do nothing;

-- 2) Daftar admin -------------------------------------------------------
create table if not exists public.admin_users (
  user_id    uuid primary key references auth.users(id) on delete cascade,
  created_at timestamptz not null default now()
);

alter table public.admin_users enable row level security;
-- Sengaja tanpa policy: tabel ini tidak bisa dibaca/ditulis lewat API.
-- Hanya fungsi security definer di bawah yang boleh menyentuhnya.

create or replace function public.is_admin()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (select 1 from public.admin_users where user_id = auth.uid());
$$;

-- 3) RLS tabel maintenance ---------------------------------------------
alter table public.game_maintenance enable row level security;

drop policy if exists "maintenance_read_all" on public.game_maintenance;
create policy "maintenance_read_all"
  on public.game_maintenance for select
  to anon, authenticated
  using (true);
-- Tidak ada policy insert/update/delete → tulis HANYA lewat RPC admin_* di bawah.

-- 4) RPC publik: status untuk game -------------------------------------
-- Waktu dihitung di SERVER (now()), jadi jam HP pemain tidak bisa dicurangi.
create or replace function public.get_maintenance_status()
returns jsonb
language sql
stable
security invoker
set search_path = public
as $$
  select jsonb_build_object(
    'active',
      (m.is_active
        and (m.starts_at is null or now() >= m.starts_at)
        and (m.ends_at   is null or now() <  m.ends_at)),
    'title',             m.title,
    'message',           m.message,
    'has_end',           (m.ends_at is not null),
    'remaining_seconds', coalesce(greatest(0, ceil(extract(epoch from (m.ends_at - now())))::int), 0),
    'block_ingame',      m.block_ingame
  )
  from public.game_maintenance m
  where m.id = 1;
$$;

grant execute on function public.get_maintenance_status() to anon, authenticated;

-- 5) RPC admin ---------------------------------------------------------
-- Mulai / ubah maintenance. Durasi dihitung dari jam server.
create or replace function public.admin_set_maintenance(
  p_active           boolean,
  p_title            text        default null,
  p_message          text        default null,
  p_duration_minutes int         default null,  -- mis. 30 → selesai 30 menit dari sekarang
  p_ends_at          timestamptz default null,  -- atau tentukan jam selesai persis
  p_starts_at        timestamptz default null,  -- opsional: jadwalkan mulai nanti
  p_block_ingame     boolean     default null
)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
  v_start timestamptz := coalesce(p_starts_at, now());
  v_end   timestamptz;
begin
  if not public.is_admin() then
    raise exception 'forbidden' using errcode = '42501';
  end if;

  if p_duration_minutes is not null then
    if p_duration_minutes <= 0 then
      raise exception 'durasi harus > 0 menit';
    end if;
    v_end := v_start + make_interval(mins => p_duration_minutes);
  else
    v_end := p_ends_at;
  end if;

  if v_end is not null and v_end <= v_start then
    raise exception 'jam selesai harus setelah jam mulai';
  end if;

  update public.game_maintenance set
    is_active    = p_active,
    title        = coalesce(nullif(btrim(p_title), ''),   title),
    message      = coalesce(nullif(btrim(p_message), ''), message),
    starts_at    = p_starts_at,
    ends_at      = v_end,
    block_ingame = coalesce(p_block_ingame, block_ingame),
    updated_at   = now(),
    updated_by   = auth.uid()
  where id = 1;

  return public.get_maintenance_status();
end;
$$;

-- Akhiri sekarang juga (pemain langsung bisa masuk).
create or replace function public.admin_end_maintenance()
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
begin
  if not public.is_admin() then
    raise exception 'forbidden' using errcode = '42501';
  end if;

  update public.game_maintenance set
    is_active  = false,
    starts_at  = null,
    ends_at    = null,
    updated_at = now(),
    updated_by = auth.uid()
  where id = 1;

  return public.get_maintenance_status();
end;
$$;

-- Perpanjang (tambah menit ke jam selesai; kalau belum ada jam selesai, hitung dari sekarang).
create or replace function public.admin_extend_maintenance(p_minutes int)
returns jsonb
language plpgsql
security definer
set search_path = public
as $$
begin
  if not public.is_admin() then
    raise exception 'forbidden' using errcode = '42501';
  end if;
  if p_minutes is null or p_minutes <= 0 then
    raise exception 'menit harus > 0';
  end if;

  update public.game_maintenance set
    is_active  = true,
    ends_at    = greatest(coalesce(ends_at, now()), now()) + make_interval(mins => p_minutes),
    updated_at = now(),
    updated_by = auth.uid()
  where id = 1;

  return public.get_maintenance_status();
end;
$$;

revoke all on function public.admin_set_maintenance(boolean, text, text, int, timestamptz, timestamptz, boolean) from public, anon;
revoke all on function public.admin_end_maintenance()          from public, anon;
revoke all on function public.admin_extend_maintenance(int)    from public, anon;
grant execute on function public.admin_set_maintenance(boolean, text, text, int, timestamptz, timestamptz, boolean) to authenticated;
grant execute on function public.admin_end_maintenance()       to authenticated;
grant execute on function public.admin_extend_maintenance(int) to authenticated;
grant execute on function public.is_admin()                    to authenticated;

-- 6) Realtime (supaya panel admin ikut update langsung) -----------------
alter table public.game_maintenance replica identity full;

do $$
begin
  if not exists (
    select 1 from pg_publication_tables
    where pubname = 'supabase_realtime' and schemaname = 'public' and tablename = 'game_maintenance'
  ) then
    alter publication supabase_realtime add table public.game_maintenance;
  end if;
end $$;

-- =====================================================================
-- 7) JADIKAN AKUN KAMU ADMIN (jalankan SETELAH bikin user di
--    Dashboard → Authentication → Users → Add user)
-- =====================================================================
-- insert into public.admin_users (user_id)
-- select id from auth.users where email = 'EMAIL_ADMIN_KAMU@contoh.com'
-- on conflict do nothing;
