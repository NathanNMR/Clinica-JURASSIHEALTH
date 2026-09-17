using ClinicaJurassica.Data;
using ClinicaJurassica.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicaJurassica.Controllers;

[ApiController, Route("api/v1")]
public class PimController : ControllerBase
{
    private readonly ClinicaContext _db;
    public PimController(ClinicaContext db) => _db = db;

    [HttpGet("status")]
    public IActionResult Status() => Ok(new { sistema="JURASSIHEALTH", online=true, utc=DateTime.UtcNow });

    [HttpGet("dashboard/resumo"), TokenAuth("adm","secretaria")]
    public async Task<IActionResult> Resumo()
    {
        var ini=DateTime.Today; var fim=ini.AddDays(1);
        return Ok(new {
            pacientes=await _db.Pacientes.CountAsync(),
            medicos=await _db.Medicos.CountAsync(),
            consultasHoje=await _db.Agendamentos.CountAsync(x=>x.DataHora>=ini && x.DataHora<fim),
            documentos=await _db.DocumentosMedicos.CountAsync()
        });
    }

    [HttpGet("auditoria"), TokenAuth("adm")]
    public async Task<IActionResult> Auditoria(int limite=100)
    {
        limite=Math.Clamp(limite,1,500);
        return Ok(await _db.Auditoria.AsNoTracking().OrderByDescending(x=>x.DataEvento).Take(limite).ToListAsync());
    }
}
