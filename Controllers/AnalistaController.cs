using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers;

[Authorize(Roles = "Analista")]
public class AnalistaController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IDistributedCache _cache;

    public AnalistaController(ApplicationDbContext db, IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var solicitudes = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .Where(s => s.Estado == EstadoSolicitud.Pendiente)
            .ToListAsync();

        // Obtener emails de los usuarios
        var userIds = solicitudes.Select(s => s.Cliente!.UsuarioId).Distinct().ToList();
        var usuarios = _db.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionary(u => u.Id, u => u.Email);

        ViewBag.Usuarios = usuarios;

        return View(solicitudes);
    }

    [HttpPost]
    public async Task<IActionResult> Aprobar(int id)
    {
        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "Solo se pueden aprobar solicitudes en estado Pendiente.";
            return RedirectToAction("Index");
        }

        if (solicitud.MontoSolicitado > solicitud.Cliente!.IngresosMensuales * 5)
        {
            TempData["Error"] = $"No se puede aprobar. El monto excede 5 veces los ingresos del cliente (máx: S/. {solicitud.Cliente.IngresosMensuales * 5}).";
            return RedirectToAction("Index");
        }

        solicitud.Estado = EstadoSolicitud.Aprobado;
        await _db.SaveChangesAsync();

        // Invalidar cache del cliente
        var cacheKey = $"solicitudes_{solicitud.Cliente.UsuarioId}";
        await _cache.RemoveAsync(cacheKey);

        TempData["Exito"] = "Solicitud aprobada exitosamente.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Rechazar(int id, string motivoRechazo)
    {
        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (solicitud == null) return NotFound();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            TempData["Error"] = "Solo se pueden rechazar solicitudes en estado Pendiente.";
            return RedirectToAction("Index");
        }

        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            TempData["Error"] = "El motivo de rechazo es obligatorio.";
            return RedirectToAction("Index");
        }

        solicitud.Estado = EstadoSolicitud.Rechazado;
        solicitud.MotivoRechazo = motivoRechazo;
        await _db.SaveChangesAsync();

        // Invalidar cache del cliente
        var cacheKey = $"solicitudes_{solicitud.Cliente!.UsuarioId}";
        await _cache.RemoveAsync(cacheKey);

        TempData["Exito"] = "Solicitud rechazada.";
        return RedirectToAction("Index");
    }
}