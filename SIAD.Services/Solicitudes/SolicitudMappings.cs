using AutoMapper;
using SIAD.Core.DTOs.Solicitudes;
using SIAD.Core.Entities;

namespace SIAD.Services.Solicitudes;

/// <summary>
/// AutoMapper profile para mapeo entre solicitud_servicio entity y DTOs.
/// </summary>
public class SolicitudMappings : Profile
{
    public SolicitudMappings()
    {
        // Entity → DetailDto
        CreateMap<solicitud_servicio, SolicitudDetailDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.solicitud_servicio_id))
            .ForMember(d => d.IdentificacionCliente, o => o.MapFrom(s => s.cliente_identidad))
            .ForMember(d => d.NombreCliente, o => o.MapFrom(s => s.cliente_nombre))
            .ForMember(d => d.CategoriaServicioId, o => o.MapFrom(s => s.categoria_servicio_id))
            .ForMember(d => d.Telefono, o => o.MapFrom(s => s.cliente_telefono))
            .ForMember(d => d.Movil, o => o.MapFrom(s => s.cliente_movil))
            .ForMember(d => d.Direccion, o => o.MapFrom(s => s.cliente_direccion))
            .ForMember(d => d.Rtn, o => o.MapFrom(s => s.cliente_rtn))
            .ForMember(d => d.Correo, o => o.MapFrom(s => s.cliente_email))
            .ForMember(d => d.Observacion, o => o.MapFrom(s => s.observacion))
            .ForMember(d => d.ColorCasa, o => o.MapFrom(s => s.cliente_color_casa))
            .ForMember(d => d.FechaNacimiento, o => o.MapFrom(s => s.fechanacimiento))
            .ForMember(d => d.ClaveSure, o => o.MapFrom(s => s.clave_sure))
            .ForMember(d => d.Fecha, o => o.MapFrom(s => s.fechacreacion))
            .ForMember(d => d.Estado, o => o.MapFrom(s => s.estado))
            .ForMember(d => d.Asignada, o => o.MapFrom(s => s.asiginada ?? false))
            .ForMember(d => d.CategoriaServicioNombre, o => o.MapFrom(s => s.categoria_servicio.descripcion))
            .ForMember(d => d.EmpresaNombre, o => o.MapFrom(s => s.empresa_nombre))
            .ForMember(d => d.EmpresaTelefono, o => o.MapFrom(s => s.empresa_telefono))
            .ForMember(d => d.EmpresaDireccion, o => o.MapFrom(s => s.empresa_direccion))
            .ForMember(d => d.NegocioNombre, o => o.MapFrom(s => s.negocio_nombre))
            .ForMember(d => d.NegocioTelefono, o => o.MapFrom(s => s.negocio_telefono))
            .ForMember(d => d.NegocioClaveCatastral, o => o.MapFrom(s => s.negocio_clave_catastral));

        // Entity → ListDto
        CreateMap<solicitud_servicio, SolicitudListDto>()
            .ForMember(d => d.Id, o => o.MapFrom(s => s.solicitud_servicio_id))
            .ForMember(d => d.IdentificacionCliente, o => o.MapFrom(s => s.cliente_identidad))
            .ForMember(d => d.NombreCliente, o => o.MapFrom(s => s.cliente_nombre))
            .ForMember(d => d.CategoriaServicioId, o => o.MapFrom(s => s.categoria_servicio_id))
            .ForMember(d => d.CategoriaServicioNombre, o => o.MapFrom(s => s.categoria_servicio.descripcion))
            .ForMember(d => d.Fecha, o => o.MapFrom(s => s.fechacreacion ?? DateTime.MinValue))
            .ForMember(d => d.Estado, o => o.MapFrom(s => s.estado))
            .ForMember(d => d.Asignada, o => o.MapFrom(s => s.asiginada ?? false));

        // CreateDto → Entity
        CreateMap<SolicitudCreateDto, solicitud_servicio>()
            .ForMember(d => d.cliente_identidad, o => o.MapFrom(s => s.IdentificacionCliente))
            .ForMember(d => d.cliente_nombre, o => o.MapFrom(s => s.NombreCliente))
            .ForMember(d => d.categoria_servicio_id, o => o.MapFrom(s => s.CategoriaServicioId))
            .ForMember(d => d.cliente_telefono, o => o.MapFrom(s => s.Telefono))
            .ForMember(d => d.cliente_movil, o => o.MapFrom(s => s.Movil))
            .ForMember(d => d.cliente_direccion, o => o.MapFrom(s => s.Direccion))
            .ForMember(d => d.cliente_rtn, o => o.MapFrom(s => s.Rtn))
            .ForMember(d => d.cliente_email, o => o.MapFrom(s => s.Correo))
            .ForMember(d => d.observacion, o => o.MapFrom(s => s.Observacion))
            .ForMember(d => d.cliente_color_casa, o => o.MapFrom(s => s.ColorCasa))
            .ForMember(d => d.fechanacimiento, o => o.MapFrom(s => s.FechaNacimiento))
            .ForMember(d => d.clave_sure, o => o.MapFrom(s => s.ClaveSure))
            .ForMember(d => d.empresa_nombre, o => o.MapFrom(s => s.EmpresaNombre))
            .ForMember(d => d.empresa_telefono, o => o.MapFrom(s => s.EmpresaTelefono))
            .ForMember(d => d.empresa_direccion, o => o.MapFrom(s => s.EmpresaDireccion))
            .ForMember(d => d.negocio_nombre, o => o.MapFrom(s => s.NegocioNombre))
            .ForMember(d => d.negocio_telefono, o => o.MapFrom(s => s.NegocioTelefono))
            .ForMember(d => d.negocio_clave_catastral, o => o.MapFrom(s => s.NegocioClaveCatastral))
            .ForMember(d => d.estado, o => o.MapFrom(s => true))
            .ForMember(d => d.asiginada, o => o.MapFrom(s => false));

        // UpdateDto → Entity
        CreateMap<SolicitudUpdateDto, solicitud_servicio>()
            .ForMember(d => d.solicitud_servicio_id, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.cliente_nombre, o => o.MapFrom(s => s.NombreCliente))
            .ForMember(d => d.categoria_servicio_id, o => o.MapFrom(s => s.CategoriaServicioId))
            .ForMember(d => d.cliente_telefono, o => o.MapFrom(s => s.Telefono))
            .ForMember(d => d.cliente_movil, o => o.MapFrom(s => s.Movil))
            .ForMember(d => d.cliente_direccion, o => o.MapFrom(s => s.Direccion))
            .ForMember(d => d.cliente_rtn, o => o.MapFrom(s => s.Rtn))
            .ForMember(d => d.cliente_email, o => o.MapFrom(s => s.Correo))
            .ForMember(d => d.observacion, o => o.MapFrom(s => s.Observacion))
            .ForMember(d => d.cliente_color_casa, o => o.MapFrom(s => s.ColorCasa))
            .ForMember(d => d.fechanacimiento, o => o.MapFrom(s => s.FechaNacimiento))
            .ForMember(d => d.clave_sure, o => o.MapFrom(s => s.ClaveSure))
            .ForMember(d => d.empresa_nombre, o => o.MapFrom(s => s.EmpresaNombre))
            .ForMember(d => d.empresa_telefono, o => o.MapFrom(s => s.EmpresaTelefono))
            .ForMember(d => d.empresa_direccion, o => o.MapFrom(s => s.EmpresaDireccion))
            .ForMember(d => d.negocio_nombre, o => o.MapFrom(s => s.NegocioNombre))
            .ForMember(d => d.negocio_telefono, o => o.MapFrom(s => s.NegocioTelefono))
            .ForMember(d => d.negocio_clave_catastral, o => o.MapFrom(s => s.NegocioClaveCatastral));
    }
}
