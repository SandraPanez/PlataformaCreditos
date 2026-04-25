using Microsoft.AspNetCore.Identity;
using PlataformaCreditos.Models;

namespace PlataformaCreditos.Data;

public static class Seeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Crear rol Analista
        if (!await roleManager.RoleExistsAsync("Analista"))
            await roleManager.CreateAsync(new IdentityRole("Analista"));

        // Crear usuario analista
        var analista = await userManager.FindByEmailAsync("analista@banco.com");
        if (analista == null)
        {
            analista = new IdentityUser { UserName = "analista@banco.com", Email = "analista@banco.com", EmailConfirmed = true };
            await userManager.CreateAsync(analista, "Analista123!");
            await userManager.AddToRoleAsync(analista, "Analista");
        }

        // Crear usuario cliente
        var clienteUser = await userManager.FindByEmailAsync("cliente@banco.com");
        if (clienteUser == null)
        {
            clienteUser = new IdentityUser { UserName = "cliente@banco.com", Email = "cliente@banco.com", EmailConfirmed = true };
            await userManager.CreateAsync(clienteUser, "Cliente123!");
        }

        // Seed clientes y solicitudes
        if (!db.Clientes.Any())
        {
            var cliente1 = new Cliente { UsuarioId = clienteUser.Id, IngresosMensuales = 3000, Activo = true };
            var cliente2 = new Cliente { UsuarioId = analista.Id, IngresosMensuales = 5000, Activo = true };
            db.Clientes.AddRange(cliente1, cliente2);
            await db.SaveChangesAsync();

            db.SolicitudesCredito.AddRange(
                new SolicitudCredito { ClienteId = cliente1.Id, MontoSolicitado = 5000, Estado = EstadoSolicitud.Pendiente },
                new SolicitudCredito { ClienteId = cliente2.Id, MontoSolicitado = 8000, Estado = EstadoSolicitud.Aprobado }
            );
            await db.SaveChangesAsync();
        }
    }
}