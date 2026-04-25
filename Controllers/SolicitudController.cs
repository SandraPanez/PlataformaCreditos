using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;
using System.Text.Json;

namespace PlataformaCreditos.Controllers;

[Authorize]
public class SolicitudController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IDistributedCache _cache;

    public SolicitudController(ApplicationDbContext db, UserManager<IdentityUser> userManager, IDistributedCache cache)
    {
        _db = db;
        _userManager = userManager;
        _cache = cache;
    }

    public async Task<IActionResult> Index(string? estado, decimal? montoMin, decimal? montoMax, DateTime? fechaInicio, DateTime? fechaFin)
    {
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

        // Cache
        bool hayFiltros = !string.IsNullOrEmpty(estado) || montoMin.HasValue || montoMax.HasValue || fechaInicio.HasValue || fechaFin.HasValue;
        string cacheKey = $"solicitudes_{userId}";
        List<SolicitudCredito> solicitudes;

        if (!hayFiltros)
        {
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                solicitudes = JsonSerializer.Deserialize<List<SolicitudCredito>>(cached)!;
            }
            else
            {
                solicitudes = await _db.SolicitudesCredito
                    .Where(s => s.ClienteId == cliente.Id)
                    .ToListAsync();

                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(solicitudes),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
                    });
            }
        }
        else
        {
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

            solicitudes = await query.ToListAsync();
        }

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

        // Guardar última solicitud visitada en sesión
        HttpContext.Session.SetString("UltimaSolicitudId", solicitud.Id.ToString());
        HttpContext.Session.SetString("UltimaSolicitudMonto", solicitud.MontoSolicitado.ToString());

        return View(solicitud);
    }

    [HttpGet]
    public async Task<IActionResult> Crear()
    {
        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null)
        {
            ViewBag.Error = "No tienes un perfil de cliente registrado.";
            return View();
        }

        if (!cliente.Activo)
        {
            ViewBag.Error = "Tu cuenta de cliente está inactiva.";
            return View();
        }

        var tienePendiente = await _db.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (tienePendiente)
        {
            ViewBag.Error = "Ya tienes una solicitud en estado Pendiente.";
            return View();
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Crear(decimal montoSolicitado)
    {
        var userId = _userManager.GetUserId(User);
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UsuarioId == userId);

        if (cliente == null)
        {
            ViewBag.Error = "No tienes un perfil de cliente registrado.";
            return View();
        }

        if (!cliente.Activo)
        {
            ViewBag.Error = "Tu cuenta de cliente está inactiva.";
            return View();
        }

        if (montoSolicitado <= 0)
        {
            ViewBag.Error = "El monto debe ser mayor a 0.";
            return View();
        }

        if (montoSolicitado > cliente.IngresosMensuales * 10)
        {
            ViewBag.Error = $"El monto no puede superar 10 veces tus ingresos mensuales (máx: S/. {cliente.IngresosMensuales * 10}).";
            return View();
        }

        var tienePendiente = await _db.SolicitudesCredito
            .AnyAsync(s => s.ClienteId == cliente.Id && s.Estado == EstadoSolicitud.Pendiente);

        if (tienePendiente)
        {
            ViewBag.Error = "Ya tienes una solicitud en estado Pendiente.";
            return View();
        }

        var solicitud = new SolicitudCredito
        {
            ClienteId = cliente.Id,
            MontoSolicitado = montoSolicitado,
            FechaSolicitud = DateTime.Now,
            Estado = EstadoSolicitud.Pendiente
        };

        _db.SolicitudesCredito.Add(solicitud);
        await _db.SaveChangesAsync();

        // Invalidar cache
        string cacheKey = $"solicitudes_{userId}";
        await _cache.RemoveAsync(cacheKey);

        ViewBag.Exito = "¡Solicitud registrada exitosamente!";
        return View();
    }
}