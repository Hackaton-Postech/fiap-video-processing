using FiapVideo.Auth.Application;
using FiapVideo.Auth.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FiapVideo.Auth.Infrastructure;

public class UsuarioRepository(AuthDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken ct)
    {
        db.Usuarios.Add(usuario);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            throw new ConflitoException("E-mail já cadastrado.");
        }
    }
}
