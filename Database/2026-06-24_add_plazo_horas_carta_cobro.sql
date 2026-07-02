-- Plazo (en horas) del requerimiento de pago en mora; conserva el número (#1/#2/#3) en reimpresiones.
ALTER TABLE cln_carta_cobro_hdr ADD COLUMN IF NOT EXISTS plazo_horas int;
