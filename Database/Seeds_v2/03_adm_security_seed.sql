-- ================================================
-- 03_adm_security_seed.sql
-- Seeds demo para seguridad usando ASP.NET Identity.
--   * Crea roles Identity base y asigna claims de permiso
--   * Registra usuarios demo (sysadmin / opcom) con contraseñas conocidas
--   * Asigna roles a usuarios
-- Requiere: 01_cfg_configuracion_seed.sql (compañía) y migraciones de Identity ejecutadas (schema identity.*).
-- ================================================

DO $$
DECLARE
    v_company_id        bigint;
    v_role_super        text := 'role-superadmin';
    v_role_admin        text := 'role-admin-oper';
    v_role_oper         text := 'role-operator';
    v_user_super        text := 'user-sysadmin';
    v_user_oper         text := 'user-opcom';
    v_perm              text;
    v_super_perms       text[] := ARRAY[
        'CONFIG.PARAMS.MANAGE',
        'ADMIN.USERS.VIEW',
        'ADMIN.USERS.MANAGE',
        'VENTAS.FACTURACION',
        'VENTAS.COBROS',
        'COMPRAS.ORDENES',
        'COMPRAS.PAGOS',
        'BANCOS.MOVIMIENTOS',
        'INVENTARIO.MAESTROS',
        'REPORTES.GLOBALES'
    ];
    v_admin_perms       text[] := ARRAY[
        'ADMIN.USERS.VIEW',
        'VENTAS.FACTURACION',
        'VENTAS.COBROS',
        'COMPRAS.ORDENES',
        'COMPRAS.PAGOS',
        'BANCOS.MOVIMIENTOS',
        'REPORTES.GLOBALES'
    ];
    v_oper_perms        text[] := ARRAY[
        'VENTAS.FACTURACION',
        'VENTAS.COBROS',
        'COMPRAS.ORDENES',
        'BANCOS.MOVIMIENTOS'
    ];
BEGIN
    -- Validar que exista la compañía demo (punto de referencia para el resto de seeds)
    SELECT company_id
      INTO v_company_id
      FROM public.cfg_company
     WHERE code = 'SIAD-DEMO'
     LIMIT 1;

    IF v_company_id IS NULL THEN
        RAISE EXCEPTION 'Company SIAD-DEMO no encontrada. Ejecuta seeds de configuración primero.';
    END IF;

    -- =============================================
    -- Roles ASP.NET Identity
    -- =============================================
    INSERT INTO identity."AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
    VALUES
        (v_role_super, 'Super Administrador', 'SUPER ADMINISTRADOR', 'CONC-ROLE-SUPER'),
        (v_role_admin, 'Administrador Operativo', 'ADMINISTRADOR OPERATIVO', 'CONC-ROLE-ADMIN'),
        (v_role_oper,  'Operador Comercial', 'OPERADOR COMERCIAL', 'CONC-ROLE-OPER')
    ON CONFLICT ("Id") DO UPDATE
        SET "Name" = EXCLUDED."Name",
            "NormalizedName" = EXCLUDED."NormalizedName";

    -- =============================================
    -- Usuarios demo (contraseñas: Admin123$ y Oper123$)
    -- Hashes generados con PasswordHasher<IdentityUser> (ASP.NET Core 9)
    -- =============================================
    INSERT INTO identity."AspNetUsers" (
        "Id", "UserName", "NormalizedUserName", "Email", "NormalizedEmail",
        "EmailConfirmed", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
        "PhoneNumber", "PhoneNumberConfirmed", "TwoFactorEnabled",
        "LockoutEnabled", "AccessFailedCount")
    VALUES
        (v_user_super, 'admin@siad-demo.com', 'ADMIN@SIAD-DEMO.COM', 'admin@siad-demo.com', 'ADMIN@SIAD-DEMO.COM',
         true, 'AQAAAAIAAYagAAAAEM+3MbF2d3g2SlKy5wLO9u+jjveNTW7G4c4e6s+Yj4PWWImUNwLzvs3rSgvW5bQ3bA==',
         'SEC-SYSADMIN', 'CONC-SYSADMIN', '+504 9999-0000', false, false, false, 0),
        (v_user_oper, 'operaciones@siad-demo.com', 'OPERACIONES@SIAD-DEMO.COM', 'operaciones@siad-demo.com', 'OPERACIONES@SIAD-DEMO.COM',
         true, 'AQAAAAIAAYagAAAAEE+Ni/1pphysdJZI37AhRLlHW1V2Y81pIBlOY8qrkTFNZIDtxS9QIiVAWyDeMh60Mg==',
         'SEC-OPCOM', 'CONC-OPCOM', '+504 8888-0000', false, false, false, 0)
    ON CONFLICT ("Id") DO UPDATE
        SET "UserName" = EXCLUDED."UserName",
            "NormalizedUserName" = EXCLUDED."NormalizedUserName",
            "Email" = EXCLUDED."Email",
            "NormalizedEmail" = EXCLUDED."NormalizedEmail",
            "PasswordHash" = EXCLUDED."PasswordHash",
            "PhoneNumber" = EXCLUDED."PhoneNumber";

    -- =============================================
    -- Asignación de roles a usuarios
    -- =============================================
    INSERT INTO identity."AspNetUserRoles" ("UserId", "RoleId")
    VALUES
        (v_user_super, v_role_super),
        (v_user_super, v_role_admin),
        (v_user_oper,  v_role_oper)
    ON CONFLICT DO NOTHING;

    -- =============================================
    -- Claims/Permisos por rol (ClaimType = 'permission')
    -- =============================================
    FOREACH v_perm IN ARRAY v_super_perms LOOP
        INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
        SELECT v_role_super, 'permission', v_perm
        WHERE NOT EXISTS (
            SELECT 1 FROM identity."AspNetRoleClaims"
             WHERE "RoleId" = v_role_super
               AND "ClaimType" = 'permission'
               AND "ClaimValue" = v_perm);
    END LOOP;

    FOREACH v_perm IN ARRAY v_admin_perms LOOP
        INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
        SELECT v_role_admin, 'permission', v_perm
        WHERE NOT EXISTS (
            SELECT 1 FROM identity."AspNetRoleClaims"
             WHERE "RoleId" = v_role_admin
               AND "ClaimType" = 'permission'
               AND "ClaimValue" = v_perm);
    END LOOP;

    FOREACH v_perm IN ARRAY v_oper_perms LOOP
        INSERT INTO identity."AspNetRoleClaims" ("RoleId", "ClaimType", "ClaimValue")
        SELECT v_role_oper, 'permission', v_perm
        WHERE NOT EXISTS (
            SELECT 1 FROM identity."AspNetRoleClaims"
             WHERE "RoleId" = v_role_oper
               AND "ClaimType" = 'permission'
               AND "ClaimValue" = v_perm);
    END LOOP;

    RAISE NOTICE 'Roles y usuarios Identity generados para compañía %, usuarios % / %',
        v_company_id, v_user_super, v_user_oper;
END
$$;
