-- Sincroniza cambios de codigo en public.servicios con servicios_roles_ws.

CREATE OR REPLACE FUNCTION public.trg_sync_servicios_roles_ws()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        IF NEW.servicios_codigo IS DISTINCT FROM OLD.servicios_codigo THEN
            UPDATE public.servicios_roles_ws
            SET servicios_codigo = NEW.servicios_codigo
            WHERE servicios_codigo = OLD.servicios_codigo;
        END IF;
        RETURN NEW;
    ELSIF TG_OP = 'DELETE' THEN
        DELETE FROM public.servicios_roles_ws
        WHERE servicios_codigo = OLD.servicios_codigo;
        RETURN OLD;
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_sync_servicios_roles_ws ON public.servicios;
CREATE TRIGGER trg_sync_servicios_roles_ws
AFTER UPDATE OR DELETE ON public.servicios
FOR EACH ROW
EXECUTE FUNCTION public.trg_sync_servicios_roles_ws();
