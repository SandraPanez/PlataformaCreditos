using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;

    public SolicitudController(ApplicationDbContext db, UserManager<IdentityUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string? estado, decimal? montoMin, decimal? montoMax, DateTime? fechaInicio, DateTime? fechaFin)
    {
        // Validaciones server-side
        if (montoMin.HasValue && montoMin < 0)
            ModelState.AddModelError("", "El monto mínimo no puede ser negativo.");

        if (montoMax.HasValue && montoMax < 0)
            ModelState.AddModelError("", "El monto máximo no puede ser negativo.");

        if (fechaInicio.HasValue && fechaFin.HasValue && fechaInicio > fechaFin)
            ModelState.AddModelError("", "La fecha inicio no puede ser mayor a la fecha fin.");

        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null)
        {
            ViewBag.Mensaje = "No tienes un perfil de cliente registrado.";
            return View(new List<SolicitudCredito>());
        }

        var query = _db.SolicitudesCredito
            .Where(s => s.ClienteId == cliente.Id)
            .AsQueryable();

        if (!string.IsNullOrEmpty(estado) && Enum.TryParse<EstadoSolicitud>(estado, out var estadoEnum))
            query = query.Where(s => s.Estado == estadoEnum);

        if (montoMin.HasValue && montoMin >= 0)
            query = query.Where(s => s.MontoSolicitado >= montoMin.Value);

        if (montoMax.HasValue && montoMax >= 0)
            query = query.Where(s => s.MontoSolicitado <= montoMax.Value);

        if (fechaInicio.HasValue && (!fechaFin.HasValue || fechaInicio <= fechaFin))
            query = query.Where(s => s.FechaSolicitud >= fechaInicio.Value);

        if (fechaFin.HasValue && (!fechaInicio.HasValue || fechaInicio <= fechaFin))
            query = query.Where(s => s.FechaSolicitud <= fechaFin.Value);

        var solicitudes = await query.ToListAsync();
        return View(solicitudes);
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null) return RedirectToAction("Index");

        var solicitud = await _db.SolicitudesCredito
            .Include(s => s.Cliente)
            .FirstOrDefaultAsync(s => s.Id == id && s.ClienteId == cliente.Id);

        if (solicitud == null) return NotFound();

        return View(solicitud);
    }
}