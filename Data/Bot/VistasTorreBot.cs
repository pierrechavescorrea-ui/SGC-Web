using System.Text;
using Microsoft.EntityFrameworkCore;
using SigWeb.Data.Pie;
using SigWeb.Data.Planes;
using SigWeb.Data.Riesgos;
using SigWeb.Models;
using static SigWeb.Data.Bot.ClienteTelegram;

namespace SigWeb.Data.Bot;

// Mensaje del bot: texto HTML + botones en línea. Al navegar se reescribe el mismo mensaje.
public sealed record VistaBot(string Html, List<List<Boton>> Teclado);

// Archivo listado en una vista: tipo (letra del callback "f:{tipo}:{id}", ver ManejadorBot.ArchivoAsync), Id y nombre.
public sealed record ArchivoBot(char Tipo, int Id, string Nombre, string? Nota = null);

// Hoja de vida de una torre en el bot. Mismas fuentes que la hoja de vida de Trazado:
// planos vinculados (ComponentePlano), ensayos (ResultadoEnsayo.ElementoTrazadoId), materiales (UbicacionMaterial de
// la torre, sus elementos o su tiro), riesgos/planes (HallazgosDeTorreAsync / PlanesDeTorreAsync), actividades
// (RegistroParametro, misma regla que CumplimientoPie) y elementos (Componente). Callbacks "t:{torre}:{vista}[:{arg}]".
public sealed class VistasTorreBot
{
    public const int PorPagina = 8;
    private readonly AppDbContext _db;
    private readonly IServiceProvider _sp;
    private readonly ServicioBotTelegram _bot;

    public VistasTorreBot(AppDbContext db, IServiceProvider sp, ServicioBotTelegram bot) { _db = db; _sp = sp; _bot = bot; }

    private sealed record Torre(ElementoTrazado E, string Codigo, Tiro? Tiro, List<Componente> Comps);

    private async Task<Torre?> CargarTorreAsync(int id)
    {
        var e = await _db.ElementosTrazado.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && !t.Eliminado);
        if (e is null) return null;
        var tiro = e.TiroId is int tid ? await _db.Tiros.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid && !t.Eliminado) : null;
        // Componente no tiene filtro global de Papelera: filtrar a mano.
        var comps = await _db.Componentes.AsNoTracking().Where(c => c.ElementoTrazadoId == id && !c.Eliminado)
            .OrderByDescending(c => c.EsHojaDeVida).ThenBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync();
        return new Torre(e, e.Codigo, tiro, comps);
    }

    public async Task<VistaBot> MostrarAsync(int torreId, string vista, int arg, int pag, int carpetaId = 0)
    {
        var t = await CargarTorreAsync(torreId);
        if (t is null) return new VistaBot("Esa torre ya no existe en SGC Web (¿se envió a la Papelera?).", new());
        return vista switch
        {
            "arc" => await ArchivosYEnsayosAsync(t),
            "hv" => await CarpetaHojaVidaAsync(t, arg, pag),
            "hc" => await CarpetaElementoAsync(t, arg, carpetaId, pag),
            "pla" => await PlanosAsync(t, pag),
            "ens" => await EnsayosAsync(t, pag),
            "mat" => await MaterialesAsync(t, pag),
            "otr" => await OtrosAsync(t, pag),
            "inf" => await InformacionAsync(t),
            "rie" => await RiesgosAsync(t, pag),
            "pln" => await PlanesAsync(t),
            "pl" => await PlanAsync(t, arg, pag),
            "act" => await ActividadesAsync(t),
            "ac" => await ActividadAsync(t, arg, pag),
            "pll" => await PlanillasAsync(t),
            "plc" => await ActividadAsync(t, arg, pag, soloPlanilla: true),
            "ele" => Elementos(t),
            "el" => await ElementoAsync(t, arg, pag),
            _ => await PortadaAsync(t),
        };
    }

    // ---------- Utilidades de presentación ----------
    private static string H(string? s) => Html(s);
    private static string Corto(string? s, int max = 90)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        s = s.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return s.Length > max ? s[..(max - 1)] + "…" : s;
    }
    private static string Pct(decimal? v) => CumplimientoPie.Formato(v);
    // Los valores de ensayo guardan fechas como yyyy-MM-dd: se muestran dd/MM/yyyy.
    private static string Fecha(string v) =>
        DateTime.TryParseExact(v, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d.ToString("dd/MM/yyyy") : v;

    // Informes (ArchivoInforme, tipo i) y adjuntos de cada resultado (AdjuntoEnsayo por valor de campo, tipo j).
    private async Task<List<ArchivoBot>> ArchivosEnsayosAsync(List<ResultadoEnsayo> res)
    {
        if (res.Count == 0) return new();
        var infIds = res.Where(r => r.InformeId != null).Select(r => r.InformeId!.Value).Distinct().ToList();
        var informes = await _db.Informes.AsNoTracking().Where(i => infIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Numero);
        var l = (await _db.ArchivosInforme.AsNoTracking().Where(a => infIds.Contains(a.InformeId)).OrderBy(a => a.InformeId).ThenBy(a => a.Id).ToListAsync())
            .Select(a => new ArchivoBot('i', a.Id, a.NombreOriginal, informes.TryGetValue(a.InformeId, out var n) ? $"Inf. {n}" : "informe")).ToList();
        var resIds = res.Select(r => r.Id).ToList();
        var adjIds = await _db.ValoresCampoEnsayo.AsNoTracking().Where(v => resIds.Contains(v.ResultadoEnsayoId) && v.AdjuntoEnsayoId != null)
            .Select(v => v.AdjuntoEnsayoId!.Value).Distinct().ToListAsync();
        if (adjIds.Count > 0)
            l.AddRange((await _db.AdjuntosEnsayo.AsNoTracking().Where(a => adjIds.Contains(a.Id)).OrderBy(a => a.NombreOriginal).ToListAsync())
                .Select(a => new ArchivoBot('j', a.Id, a.NombreOriginal, "adjunto de ensayo")));
        return l;
    }
    private static string Barra(decimal? v)
    {
        if (v is null) return "";
        var n = (int)Math.Round(Math.Clamp(v.Value, 0, 100) / 10m);
        return new string('▰', n) + new string('▱', 10 - n);
    }
    private static string Ruta(Torre t, params string[] tramos) =>
        $"<b>🗼 {H(t.Codigo)}</b>" + (tramos.Length > 0 ? " › " + string.Join(" › ", tramos.Select(H)) : "") + "\n";
    private static List<Boton> Nav(int torre, string? volver, bool inicio = true)
    {
        var f = new List<Boton>();
        if (volver is not null) f.Add(new("⬅️ Volver", volver));
        if (inicio) f.Add(new("🏠 Inicio", $"t:{torre}"));
        return f;
    }

    // Lista numerada de archivos con botones 📥 (paginada). baseDatos = callback sin la página.
    public static void Archivos(StringBuilder sb, List<List<Boton>> kb, List<ArchivoBot> archivos, int pag, string baseDatos, string titulo = "Archivos")
    {
        if (archivos.Count == 0) return;
        var paginas = (archivos.Count + PorPagina - 1) / PorPagina;
        pag = Math.Clamp(pag, 0, paginas - 1);
        sb.Append($"\n<b>{H(titulo)}</b>" + (paginas > 1 ? $" ({pag + 1}/{paginas})" : "") + "\n");
        var vista = archivos.Skip(pag * PorPagina).Take(PorPagina).ToList();
        for (int i = 0; i < vista.Count; i++)
        {
            var a = vista[i];
            sb.Append($"{pag * PorPagina + i + 1}. {H(Corto(a.Nombre, 60))}" + (string.IsNullOrEmpty(a.Nota) ? "" : $" · <i>{H(a.Nota)}</i>") + "\n");
        }
        var botones = vista.Select((a, i) => new Boton($"📥 {pag * PorPagina + i + 1}", $"f:{a.Tipo}:{a.Id}")).ToList();
        foreach (var fila in botones.Chunk(4)) kb.Add(fila.ToList());
        if (paginas > 1)
        {
            var nav = new List<Boton>();
            if (pag > 0) nav.Add(new("◀️", $"{baseDatos}:{pag - 1}"));
            nav.Add(new($"{pag + 1}/{paginas}", $"{baseDatos}:{pag}"));
            if (pag < paginas - 1) nav.Add(new("▶️", $"{baseDatos}:{pag + 1}"));
            kb.Add(nav);
        }
    }

    // ---------- Portada ----------
    private async Task<VistaBot> PortadaAsync(Torre t)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>🗼 {H(t.Codigo)} · Hoja de vida</b>\n");
        if (!string.IsNullOrWhiteSpace(t.E.Nombre) && !string.Equals(t.E.Nombre, t.Codigo, StringComparison.OrdinalIgnoreCase))
            sb.Append($"{H(t.E.Nombre)}\n");

        var avance = await _bot.AvanceAsync(_db);
        var ta = avance?.Torres.FirstOrDefault(x => x.Id == t.E.Id);
        var linea = new List<string>();
        if (!string.IsNullOrWhiteSpace(ta?.TipoTexto)) linea.Add(H(ta!.TipoTexto));
        if (t.Tiro is not null) linea.Add($"Tiro {H(t.Tiro.Nombre)}");
        if (linea.Count > 0) sb.Append(string.Join(" · ", linea) + "\n");
        if (ta?.Perfil is { } pf)
        {
            var l2 = new List<string>();
            if (pf.Abscisa is double ab) l2.Add($"K{(int)(ab / 1000)}+{ab % 1000:000}");
            if (pf.Cota is double co) l2.Add($"{co:N1} msnm");
            if (pf.AlturaTotal is double alt) l2.Add($"altura {alt:0.#} m");
            if (l2.Count > 0) sb.Append(string.Join(" · ", l2) + "\n");
        }

        var estado = await _db.EstadosTorreManual.AsNoTracking().Where(x => x.ElementoTrazadoId == t.E.Id)
            .OrderByDescending(x => x.Desde).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
        decimal? pctAvance = ta is null || avance is null ? null : avance.PctTorre(ta, DateTime.Today);
        var pie = CumplimientoPie.DeTorre(_db, t.E.Id);

        sb.Append('\n');
        sb.Append($"🏗 Avance de obra: <b>{Pct(pctAvance)}</b>\n{Barra(pctAvance)}\n");
        sb.Append($"✅ Cumplimiento PIE: <b>{Pct(pie.Pct)}</b>\n{Barra(pie.Pct)}\n");
        if (estado is not null) sb.Append($"🟧 Estado: <b>{H(estado.Estado)}</b>{(estado.Alerta ? " 🔴" : "")} (desde {estado.Desde:dd/MM/yyyy})\n");

        var riesgos = await _sp.GetRequiredService<MatrizRiesgosServicio>().HallazgosDeTorreAsync(t.E.Id);
        var planes = await _sp.GetRequiredService<PlanesAccionServicio>().PlanesDeTorreAsync(t.E.Id);
        var rAbiertos = riesgos.Count(r => r.Hallazgo.Estado != EstadoHallazgo.Cerrado);
        var pAbiertos = planes.Count(p => p.Estado != EstadoAccionPlan.Cerrada);
        sb.Append($"\n⚠️ Hallazgos de riesgo abiertos: <b>{rAbiertos}</b> · 📋 Planes abiertos: <b>{pAbiertos}</b>");

        var kb = new List<List<Boton>>
        {
            new() { new("📁 Archivos y ensayos", $"t:{t.E.Id}:arc"), new("ℹ️ Información", $"t:{t.E.Id}:inf") },
            new() { new($"⚠️ Riesgos ({rAbiertos})", $"t:{t.E.Id}:rie"), new($"📋 Planes ({pAbiertos})", $"t:{t.E.Id}:pln") },
            new() { new("🔧 Actividades", $"t:{t.E.Id}:act"), new("🦵 Elementos", $"t:{t.E.Id}:ele") },
            new() { new("📝 Planillas", $"t:{t.E.Id}:pll") },
        };
        var ultima = new List<Boton>();
        if (t.E.Latitud != 0 || t.E.Longitud != 0) ultima.Add(new("📍 Ubicación", $"t:{t.E.Id}:loc"));
        if (_bot.UrlSigWeb is { } url) ultima.Add(new("🌐 Abrir en SGC Web", Url: $"{url}/trazado?torre={t.E.Id}&hdv=true"));
        if (ultima.Count > 0) kb.Add(ultima);
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Archivos y ensayos ----------
    private async Task<VistaBot> ArchivosYEnsayosAsync(Torre t)
    {
        var archivos = (await ArchivosCarpetaAsync(t)).Count;
        var ensayos = await _db.ResultadosEnsayo.AsNoTracking().CountAsync(r => r.ElementoTrazadoId == t.E.Id && !r.Eliminado);
        var materiales = (await MaterialesDeAsync(t, null)).Select(m => m.MaterialId).Distinct().Count();
        var sb = new StringBuilder(Ruta(t, "Archivos y ensayos"));
        sb.Append($"\n📁 Archivos y planos: <b>{archivos}</b>\n🧪 Resultados de ensayos: <b>{ensayos}</b>\n🔩 Materiales: <b>{materiales}</b>");
        var kb = new List<List<Boton>>
        {
            new() { new($"📁 Archivos ({archivos})", $"t:{t.E.Id}:otr") },
            new() { new($"🧪 Ensayos ({ensayos})", $"t:{t.E.Id}:ens"), new($"🔩 Materiales ({materiales})", $"t:{t.E.Id}:mat") },
            Nav(t.E.Id, null),
        };
        var carpetas = await _db.CarpetasHojaVida.AsNoTracking().Where(c => c.Activo && c.ClaveElemento == null).OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync();
        var conteos = await _db.ArchivosCarpetaHojaVida.AsNoTracking().Where(a => a.ElementoTrazadoId == t.E.Id && a.ComponenteId == null)
            .GroupBy(a => a.CarpetaHojaVidaId).Select(g => new { Id = g.Key, Cantidad = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Cantidad);
        foreach (var carpeta in carpetas)
            kb.Insert(kb.Count - 1, new() { new($"📁 {Corto(carpeta.Nombre, 45)} ({conteos.GetValueOrDefault(carpeta.Id)})", $"t:{t.E.Id}:hv:{carpeta.Id}:0") });
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> CarpetaHojaVidaAsync(Torre t, int carpetaId, int pag)
    {
        var carpeta = await _db.CarpetasHojaVida.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carpetaId && c.Activo && c.ClaveElemento == null);
        if (carpeta is null) return await ArchivosYEnsayosAsync(t);
        var lista = await _db.ArchivosCarpetaHojaVida.AsNoTracking().Where(a => a.ElementoTrazadoId == t.E.Id && a.ComponenteId == null && a.CarpetaHojaVidaId == carpetaId)
            .OrderByDescending(a => a.FechaCarga).ThenBy(a => a.Id).Select(a => new ArchivoBot('h', a.Id, a.NombreOriginal, a.Descripcion)).ToListAsync();
        var sb = new StringBuilder(Ruta(t, "Archivos y ensayos", carpeta.Nombre));
        var kb = new List<List<Boton>>();
        if (lista.Count == 0) sb.Append("\n<i>Sin archivos cargados.</i>");
        Archivos(sb, kb, lista, pag, $"t:{t.E.Id}:hv:{carpetaId}", carpeta.Nombre);
        kb.Add(new() { new("⬅️ Archivos y ensayos", $"t:{t.E.Id}:arc") });
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> CarpetaElementoAsync(Torre t, int componenteId, int carpetaId, int pag)
    {
        var componente = t.Comps.FirstOrDefault(c => c.Id == componenteId && !c.EsHojaDeVida);
        if (componente is null) return Elementos(t);
        var clave = AmbitoCarpetasHojaVida.ClaveElemento(componente.Nombre);
        var carpeta = await _db.CarpetasHojaVida.AsNoTracking().FirstOrDefaultAsync(c => c.Id == carpetaId && c.Activo && c.ClaveElemento == clave);
        if (carpeta is null) return await ElementoAsync(t, componenteId, 0);
        var archivos = await _db.ArchivosCarpetaHojaVida.AsNoTracking()
            .Where(a => a.ElementoTrazadoId == t.E.Id && a.ComponenteId == componenteId && a.CarpetaHojaVidaId == carpetaId)
            .OrderByDescending(a => a.FechaCarga).ThenBy(a => a.Id)
            .Select(a => new ArchivoBot('h', a.Id, a.NombreOriginal, a.Descripcion)).ToListAsync();
        var sb = new StringBuilder(Ruta(t, "Elementos", componente.Nombre, carpeta.Nombre));
        var kb = new List<List<Boton>>();
        if (archivos.Count == 0) sb.Append("\n<i>Sin archivos cargados.</i>");
        Archivos(sb, kb, archivos, pag, $"t:{t.E.Id}:hc:{componenteId}:{carpetaId}", carpeta.Nombre);
        kb.Add(new() { new("⬅️ Elemento", $"t:{t.E.Id}:el:{componenteId}") });
        return new VistaBot(sb.ToString(), kb);
    }

    private sealed record PlanoVinc(ArchivoPlanoMaestro A, PlanoMaestro P, string Elemento);

    private async Task<List<PlanoVinc>> PlanosDeAsync(List<int> compIds)
    {
        var vinc = await _db.ComponentesPlano.AsNoTracking().Where(v => compIds.Contains(v.ComponenteId)).ToListAsync();
        if (vinc.Count == 0) return new();
        var idsA = vinc.Select(v => v.ArchivoPlanoMaestroId).Distinct().ToList();
        var arch = await _db.ArchivosPlanoMaestro.AsNoTracking().Where(a => idsA.Contains(a.Id) && !a.Eliminado).ToDictionaryAsync(a => a.Id);
        var idsP = arch.Values.Select(a => a.PlanoMaestroId).Distinct().ToList();
        var planos = await _db.PlanosMaestro.AsNoTracking().Where(p => idsP.Contains(p.Id) && !p.Eliminado).ToDictionaryAsync(p => p.Id);
        var nombres = await _db.Componentes.AsNoTracking().Where(c => compIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.EsHojaDeVida ? "Torre" : c.Nombre);
        return vinc.Where(v => arch.ContainsKey(v.ArchivoPlanoMaestroId) && planos.ContainsKey(arch[v.ArchivoPlanoMaestroId].PlanoMaestroId))
            .GroupBy(v => v.ArchivoPlanoMaestroId)
            .Select(g =>
            {
                var a = arch[g.Key];
                return new PlanoVinc(a, planos[a.PlanoMaestroId], string.Join(", ", g.Select(v => nombres.GetValueOrDefault(v.ComponenteId, "")).Distinct()));
            })
            .OrderBy(x => x.P.Codigo).ThenBy(x => x.A.NombreOriginal).ToList();
    }

    private static ArchivoBot ArchivoPlano(PlanoVinc x) => new('p', x.A.Id,
        $"{x.P.Codigo} {x.P.Titulo}".Trim(),
        string.Join(" · ", new[] { string.IsNullOrWhiteSpace(x.A.Version) ? null : "v" + x.A.Version.Trim(),
            x.A.Clase == ClaseArchivoDocumento.Obsoleta ? "OBSOLETO" : null, x.Elemento }.Where(s => !string.IsNullOrEmpty(s))));

    private async Task<VistaBot> PlanosAsync(Torre t, int pag)
    {
        var lista = await PlanosDeAsync(t.Comps.Select(c => c.Id).ToList());
        var sb = new StringBuilder(Ruta(t, "Archivos y ensayos", "Planos"));
        var kb = new List<List<Boton>>();
        if (lista.Count == 0) sb.Append("\nNo hay planos vinculados a la torre ni a sus elementos.");
        Archivos(sb, kb, lista.Select(ArchivoPlano).ToList(), pag, $"t:{t.E.Id}:pla", "Planos vinculados");
        // Ficha de cada plano (vigentes y versiones anteriores), sin repetir.
        var fichas = lista.Select(x => x.P).DistinctBy(p => p.Id).Take(6).Select(p => new Boton($"🗂 {Corto(p.Codigo, 18)}", $"p:{p.Id}")).ToList();
        foreach (var fila in fichas.Chunk(3)) kb.Add(fila.ToList());
        kb.Add(Nav(t.E.Id, $"t:{t.E.Id}:arc"));
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> EnsayosAsync(Torre t, int pag)
    {
        var res = await _db.ResultadosEnsayo.AsNoTracking().Where(r => r.ElementoTrazadoId == t.E.Id && !r.Eliminado).ToListAsync();
        var sb = new StringBuilder(Ruta(t, "Archivos y ensayos", "Ensayos"));
        var kb = new List<List<Boton>>();
        if (res.Count == 0) sb.Append("\nNo hay resultados de ensayos de esta torre.");
        else
        {
            var tipoIds = res.Select(r => r.TipoEnsayoId).Distinct().ToList();
            var tipos = await _db.TiposEnsayo.AsNoTracking().Where(x => tipoIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Nombre);
            var infIds = res.Where(r => r.InformeId != null).Select(r => r.InformeId!.Value).Distinct().ToList();
            var informes = await _db.Informes.AsNoTracking().Where(i => infIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id);
            var resIds = res.Select(r => r.Id).ToList();
            var vinc = await _db.ResultadosEnsayoComponente.AsNoTracking().Where(v => resIds.Contains(v.ResultadoEnsayoId)).ToListAsync();
            var nombreComp = t.Comps.ToDictionary(c => c.Id, c => c.Nombre);
            sb.Append('\n');
            foreach (var g in res.GroupBy(r => r.TipoEnsayoId).OrderBy(g => tipos.GetValueOrDefault(g.Key, "")))
            {
                var ult = g.OrderByDescending(r => r.FechaEjecucion ?? r.FechaCreacion).First();
                var patas = vinc.Where(v => g.Any(r => r.Id == v.ResultadoEnsayoId)).Select(v => nombreComp.GetValueOrDefault(v.ComponenteId)).Where(n => n != null).Distinct().ToList();
                sb.Append($"🧪 <b>{H(tipos.GetValueOrDefault(g.Key, "Ensayo"))}</b>: {g.Count()} resultado(s)");
                if (ult.FechaEjecucion is DateTime f) sb.Append($", último {f:dd/MM/yyyy}");
                if (patas.Count > 0) sb.Append($" · {H(string.Join(", ", patas!))}");
                sb.Append('\n');
            }
            Archivos(sb, kb, await ArchivosEnsayosAsync(res), pag, $"t:{t.E.Id}:ens", "Informes y adjuntos");
        }
        kb.Add(Nav(t.E.Id, $"t:{t.E.Id}:arc"));
        return new VistaBot(sb.ToString(), kb);
    }

    private sealed record MaterialUbic(int MaterialId, string Nombre, string Lugar);

    // Materiales de la torre (hoja de vida: torre, sus elementos y su tiro) o de un elemento.
    private async Task<List<MaterialUbic>> MaterialesDeAsync(Torre t, int? compId)
    {
        var compIds = t.Comps.Select(c => c.Id).ToList();
        var tiroId = t.Tiro?.Id;
        var ubic = compId is int c0
            ? await _db.UbicacionesMaterial.AsNoTracking().Where(u => u.ComponenteId == c0).ToListAsync()
            : await _db.UbicacionesMaterial.AsNoTracking().Where(u => u.ElementoTrazadoId == t.E.Id
                    || (u.ComponenteId != null && compIds.Contains(u.ComponenteId.Value)) || (tiroId != null && u.TiroId == tiroId)).ToListAsync();
        if (ubic.Count == 0) return new();
        var ids = ubic.Select(u => u.MaterialId).Distinct().ToList();
        var mats = await _db.Materiales.AsNoTracking().Where(m => ids.Contains(m.Id) && !m.Eliminado).ToDictionaryAsync(m => m.Id);
        var nombreComp = t.Comps.ToDictionary(c => c.Id, c => c.EsHojaDeVida ? "Torre" : c.Nombre);
        var resumen = await SigWeb.Data.Tablas.ResumenCampos.TextoAsync(_db, "materiales", ids);   // campos marcados como Resumen
        return ubic.Where(u => mats.ContainsKey(u.MaterialId))
            .Select(u => new MaterialUbic(u.MaterialId,
                (string.IsNullOrWhiteSpace(mats[u.MaterialId].NombreMaterial) ? $"Material #{u.MaterialId}" : mats[u.MaterialId].NombreMaterial)
                    + (resumen.TryGetValue(u.MaterialId, out var rs) ? " · " + rs : ""),
                u.TiroId != null ? "Tiro" : u.ComponenteId is int ci ? nombreComp.GetValueOrDefault(ci, "Elemento") : "Torre"))
            .OrderBy(m => m.Nombre).ToList();
    }

    private async Task<List<ArchivoBot>> AdjuntosMaterialesAsync(List<MaterialUbic> mats)
    {
        var ids = mats.Select(m => m.MaterialId).Distinct().ToList();
        var nombre = mats.GroupBy(m => m.MaterialId).ToDictionary(g => g.Key, g => g.First().Nombre);
        var adj = await _db.AdjuntosMaterial.AsNoTracking().Where(a => ids.Contains(a.MaterialId) && !a.Eliminado).ToListAsync();
        return adj.OrderBy(a => nombre.GetValueOrDefault(a.MaterialId, "")).ThenBy(a => Array.IndexOf(AdjuntoMaterial.Clases, a.Clase)).ThenByDescending(a => a.FechaCarga)
            .Select(a => new ArchivoBot('m', a.Id, a.NombreOriginal, $"{Corto(nombre.GetValueOrDefault(a.MaterialId, ""), 25)} · {a.Clase}")).ToList();
    }

    private async Task<VistaBot> MaterialesAsync(Torre t, int pag)
    {
        var mats = await MaterialesDeAsync(t, null);
        var sb = new StringBuilder(Ruta(t, "Archivos y ensayos", "Materiales"));
        var kb = new List<List<Boton>>();
        if (mats.Count == 0) sb.Append("\nNo hay materiales ligados a la torre, sus elementos o su tiro.");
        else
        {
            sb.Append('\n');
            foreach (var g in mats.GroupBy(m => m.MaterialId).Take(15))
                sb.Append($"🔩 {H(Corto(g.First().Nombre, 50))} · <i>{H(string.Join(", ", g.Select(m => m.Lugar).Distinct()))}</i>\n");
            if (mats.Select(m => m.MaterialId).Distinct().Count() > 15) sb.Append("…\n");
            Archivos(sb, kb, await AdjuntosMaterialesAsync(mats), pag, $"t:{t.E.Id}:mat", "Adjuntos");
        }
        kb.Add(Nav(t.E.Id, $"t:{t.E.Id}:arc"));
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<List<ArchivoBot>> OtrosDeAsync(List<int> compIds)
    {
        var nombres = await _db.Componentes.AsNoTracking().Where(c => compIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.EsHojaDeVida ? "Torre" : c.Nombre);
        return (await _db.ArchivosComponente.AsNoTracking().Where(a => compIds.Contains(a.ComponenteId) && !a.Eliminado).OrderBy(a => a.ComponenteId).ThenBy(a => a.Id).ToListAsync())
            .Select(a => new ArchivoBot('c', a.Id, a.NombreOriginal, nombres.GetValueOrDefault(a.ComponenteId))).ToList();
    }

    // Carpeta "Archivos" de la hoja de vida: archivos de la torre y sus elementos + planos vinculados del listado maestro.
    private async Task<List<ArchivoBot>> ArchivosCarpetaAsync(Torre t)
    {
        var ids = t.Comps.Select(c => c.Id).ToList();
        var l = await OtrosDeAsync(ids);
        l.AddRange((await PlanosDeAsync(ids)).Select(ArchivoPlano));
        return l;
    }

    private async Task<VistaBot> OtrosAsync(Torre t, int pag)
    {
        var lista = await ArchivosCarpetaAsync(t);
        var sb = new StringBuilder(Ruta(t, "Archivos y ensayos", "Archivos"));
        var kb = new List<List<Boton>>();
        if (lista.Count == 0) sb.Append("\nNo hay archivos ni planos en la torre ni en sus elementos.");
        Archivos(sb, kb, lista, pag, $"t:{t.E.Id}:otr", "Archivos y planos");
        kb.Add(Nav(t.E.Id, $"t:{t.E.Id}:arc"));
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Información (campos de la tabla de torres) ----------
    private async Task<VistaBot> InformacionAsync(Torre t)
    {
        var campos = await _db.CamposExtraTorre.AsNoTracking().Where(c => c.ElementoTrazadoId == t.E.Id && !c.Eliminado)
            .OrderBy(c => c.Id).ToListAsync();
        var sb = new StringBuilder(Ruta(t, "Información"));
        sb.Append('\n');
        if (t.Tiro is not null) sb.Append($"Tiro: <b>{H(t.Tiro.Nombre)}</b>\n");
        if (t.E.Latitud != 0 || t.E.Longitud != 0)
            sb.Append($"Coordenadas: {t.E.Latitud.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}, {t.E.Longitud.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)}\n");
        foreach (var c in campos.Where(c => !string.IsNullOrWhiteSpace(c.Valor)).DistinctBy(c => c.Clave))
        {
            if (sb.Length > 3600) { sb.Append("…\n"); break; }
            sb.Append($"{H(Corto(c.Clave, 40))}: <b>{H(Corto(c.Valor, 80))}</b>\n");
        }
        if (campos.Count == 0) sb.Append("<i>Sin datos en la tabla de torres.</i>\n");
        return new VistaBot(sb.ToString(), new() { Nav(t.E.Id, null) });
    }

    // ---------- Riesgos ----------
    private async Task<VistaBot> RiesgosAsync(Torre t, int pag)
    {
        var todos = await _sp.GetRequiredService<MatrizRiesgosServicio>().HallazgosDeTorreAsync(t.E.Id);
        var lista = todos.Where(r => r.Hallazgo.Estado != EstadoHallazgo.Cerrado).ToList();
        var sb = new StringBuilder(Ruta(t, "Riesgos"));
        var kb = new List<List<Boton>>();
        if (lista.Count == 0) sb.Append($"\nNo hay hallazgos de riesgo abiertos (cerrados: {todos.Count}).");
        else
        {
            const int n = 5;
            var paginas = (lista.Count + n - 1) / n;
            pag = Math.Clamp(pag, 0, paginas - 1);
            sb.Append(paginas > 1 ? $"<i>Hallazgos abiertos {pag + 1}/{paginas}</i>\n" : "");
            var archivos = new List<ArchivoBot>();
            foreach (var r in lista.Skip(pag * n).Take(n))
            {
                var nivel = (int)r.Probabilidad * (int)r.Impacto;
                var icono = nivel >= 15 ? "🔴" : nivel >= 8 ? "🟠" : "🟡";
                sb.Append($"\n{icono} <b>{H(r.CodigoRiesgo)}</b> {H(Corto(r.DescripcionRiesgo, 70))}\n");
                sb.Append($"   P{(int)r.Probabilidad}×I{(int)r.Impacto} = {nivel} · {H(Nombre(r.Resultado))} · {r.FechaSeguimiento:dd/MM/yyyy}\n");
                if (!string.IsNullOrWhiteSpace(r.Hallazgo.Descripcion)) sb.Append($"   Hallazgo: {H(Corto(r.Hallazgo.Descripcion, 120))}\n");
                if (!string.IsNullOrWhiteSpace(r.DondeTexto)) sb.Append($"   Dónde: {H(r.DondeTexto)}\n");
                if (r.Planes.Count > 0) sb.Append($"   Planes: {H(string.Join(", ", r.Planes.Select(p => p.Plan.Codigo)))}\n");
                archivos.AddRange(r.Archivos.Select(a => new ArchivoBot('k', a.Id, a.NombreOriginal, r.CodigoRiesgo)));
            }
            // Soportes de los hallazgos de esta página (máx. una página de botones; sin paginación propia).
            Archivos(sb, kb, archivos.DistinctBy(a => a.Id).Take(PorPagina).ToList(), 0, $"t:{t.E.Id}:rie:{pag}", "Soportes");
            if (paginas > 1)
            {
                var nav = new List<Boton>();
                if (pag > 0) nav.Add(new("◀️ Anteriores", $"t:{t.E.Id}:rie:{pag - 1}"));
                if (pag < paginas - 1) nav.Add(new("Siguientes ▶️", $"t:{t.E.Id}:rie:{pag + 1}"));
                kb.Add(nav);
            }
        }
        kb.Add(Nav(t.E.Id, null));
        return new VistaBot(sb.ToString(), kb);
    }

    private static string Nombre(ResultadoSeguimiento r) => r switch
    {
        ResultadoSeguimiento.Materializado => "Materializado",
        ResultadoSeguimiento.EnObservacion => "En observación",
        _ => "No materializado"
    };

    // ---------- Planes de acción ----------
    private static string Icono(EstadoAccionPlan e) => e switch { EstadoAccionPlan.Cerrada => "✅", EstadoAccionPlan.EnEjecucion => "⏳", _ => "🔴" };

    private async Task<VistaBot> PlanesAsync(Torre t)
    {
        var planes = await _sp.GetRequiredService<PlanesAccionServicio>().PlanesDeTorreAsync(t.E.Id);
        var sb = new StringBuilder(Ruta(t, "Planes de acción"));
        var kb = new List<List<Boton>>();
        if (planes.Count == 0) sb.Append("\nNo hay planes de acción ligados a la torre, sus elementos o su tiro.");
        sb.Append('\n');
        foreach (var p in planes.OrderBy(p => p.Estado == EstadoAccionPlan.Cerrada).ThenByDescending(p => p.Plan.Fecha).Take(12))
            sb.Append($"{Icono(p.Estado)} <b>{H(p.Plan.Codigo)}</b> {H(Corto(p.Plan.DescripcionHallazgo, 70))}\n");
        if (planes.Count > 12) sb.Append($"… y {planes.Count - 12} más\n");
        var botones = planes.OrderBy(p => p.Estado == EstadoAccionPlan.Cerrada).ThenByDescending(p => p.Plan.Fecha).Take(12)
            .Select(p => new Boton($"{Icono(p.Estado)} {Corto(p.Plan.Codigo, 14)}", $"t:{t.E.Id}:pl:{p.Plan.Id}")).ToList();
        foreach (var fila in botones.Chunk(3)) kb.Add(fila.ToList());
        kb.Add(Nav(t.E.Id, null));
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> PlanAsync(Torre t, int planId, int pag)
    {
        var p = (await _sp.GetRequiredService<PlanesAccionServicio>().PlanesDeTorreAsync(t.E.Id)).FirstOrDefault(x => x.Plan.Id == planId);
        if (p is null) return new VistaBot("Ese plan ya no está ligado a la torre.", new() { Nav(t.E.Id, $"t:{t.E.Id}:pln") });
        var sb = new StringBuilder(Ruta(t, "Planes", p.Plan.Codigo));
        sb.Append($"\n{Icono(p.Estado)} <b>{H(EscalaAccion.Nombre(p.Estado))}</b> · {p.Plan.Fecha:dd/MM/yyyy}\n");
        sb.Append($"{H(Corto(p.Plan.DescripcionHallazgo, 300))}\n");
        if (p.RiesgoCodigo is not null) sb.Append($"Riesgo: {H(p.RiesgoCodigo)}\n");
        if (p.Donde.Count > 0) sb.Append($"Dónde: {H(string.Join(", ", p.Donde))}\n");
        sb.Append("\n<b>Acciones</b>\n");
        foreach (var a in p.Acciones.OrderBy(a => a.Orden))
        {
            var vencida = a.Estado != EstadoAccionPlan.Cerrada && a.Fecha.Date < DateTime.Today ? " ⚠️ vencida" : "";
            sb.Append($"{Icono(a.Estado)} {H(Corto(a.Descripcion, 90))} · {a.Fecha:dd/MM/yyyy}{vencida}" +
                      (string.IsNullOrWhiteSpace(a.Responsable) ? "" : $" · {H(Corto(a.Responsable, 30))}") + "\n");
        }
        var anexos = p.Anexos.Values.SelectMany(l => l)
            .Concat(p.Productos.Values.SelectMany(l => l.SelectMany(x => x.Anexos)))
            .DistinctBy(a => a.Id).Select(a => new ArchivoBot('a', a.Id, a.NombreOriginal)).ToList();
        var kb = new List<List<Boton>>();
        Archivos(sb, kb, anexos, pag, $"t:{t.E.Id}:pl:{planId}", "Anexos");
        var fin = Nav(t.E.Id, $"t:{t.E.Id}:pln");
        if (_bot.UrlSigWeb is { } url) fin.Add(new("🌐 SGC Web", Url: $"{url}/planes-accion/{planId}"));
        kb.Add(fin);
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Actividades (parámetros de la Matriz PIE) ----------
    private async Task<List<Actividad>> ActividadesOrdenadasAsync()
    {
        var acts = await _db.Actividades.AsNoTracking().Where(a => a.Activo && !a.Eliminado).ToListAsync();
        var orden = (await _db.Configuracion.AsNoTracking().Select(c => c.OrdenPestanasFicha).FirstOrDefaultAsync() ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        int Pos(Actividad a) { var i = orden.IndexOf("a" + a.Id); return i < 0 ? int.MaxValue : i; }
        return acts.OrderBy(Pos).ThenBy(a => a.Ambito switch { AmbitoActividad.Tiro => 2, AmbitoActividad.Torre => 0, _ => 1 })
            .ThenBy(a => a.Orden).ThenBy(a => a.Id).ToList();
    }

    private async Task<VistaBot> ActividadesAsync(Torre t)
    {
        var detalle = CumplimientoPie.Detalle(_db, t.E.Id);
        var pctPorNombre = detalle.Actividades.GroupBy(a => (a.Nombre, a.Ambito)).ToDictionary(g => g.Key, g => g.First());
        // Solo las actividades de la hoja de vida (ámbito torre, como en Trazado), sin las de ensayos: los ensayos
        // ya están en "Archivos y ensayos" (y los de cada pata en su ficha de Elementos).
        var acts = (await ActividadesOrdenadasAsync())
            .Where(a => a.Ambito == AmbitoActividad.Torre && a.TipoEnsayoId is null
                        && !a.Nombre.TrimStart().StartsWith("Ensayo", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var sb = new StringBuilder(Ruta(t, "Actividades"));
        sb.Append($"\nCumplimiento PIE: <b>{Pct(detalle.Total.Pct)}</b>\n\n");
        var kb = new List<List<Boton>>();
        var botones = new List<Boton>();
        foreach (var a in acts)
        {
            if (a.Ambito == AmbitoActividad.Tiro && t.Tiro is null) continue;
            pctPorNombre.TryGetValue((a.Nombre, a.Ambito), out var d);
            var ic = d?.Pct is null ? "▫️" : d.Pct >= 100 ? "✅" : d.Pct > 0 ? "⏳" : "🔴";
            sb.Append($"{ic} {H(a.Nombre)}" + (d is null ? "" : $" · <b>{Pct(d.Pct)}</b>") + "\n");
            botones.Add(new Boton($"{ic} {Corto(a.Nombre, 18)}", $"t:{t.E.Id}:ac:{a.Id}"));
        }
        if (botones.Count == 0) sb.Append("<i>No hay actividades configuradas.</i>\n");
        foreach (var fila in botones.Chunk(2)) kb.Add(fila.ToList());
        kb.Add(Nav(t.E.Id, null));
        return new VistaBot(sb.ToString(), kb);
    }

    // Planillas de campo: actividades con sub actividades marcadas "Planilla" (Actividades) que aplican a la torre.
    private async Task<VistaBot> PlanillasAsync(Torre t)
    {
        var marcados = await _db.ParametrosActividad.AsNoTracking().Where(p => p.EnPlanilla && p.Activo && !p.Eliminado)
            .GroupBy(p => p.ActividadId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        var acts = (await ActividadesOrdenadasAsync()).Where(a => marcados.Any(m => m.Key == a.Id))
            .Where(a => a.Ambito != AmbitoActividad.Tiro || t.Tiro is not null).ToList();
        var sb = new StringBuilder(Ruta(t, "Planillas"));
        var kb = new List<List<Boton>>();
        if (acts.Count == 0)
            sb.Append("\nNo hay sub actividades marcadas como planilla. Se marcan en SGC Web › Configuración › Actividades (columna «Planilla»).");
        else
        {
            sb.Append("\nPlanillas de campo por actividad:\n\n");
            foreach (var a in acts)
            {
                var n = marcados.First(m => m.Key == a.Id).N;
                sb.Append($"📝 {H(a.Nombre)} · {n} planilla{(n == 1 ? "" : "s")}\n");
            }
            foreach (var fila in acts.Select(a => new Boton($"📝 {Corto(a.Nombre, 20)}", $"t:{t.E.Id}:plc:{a.Id}")).Chunk(2)) kb.Add(fila.ToList());
        }
        kb.Add(Nav(t.E.Id, null));
        return new VistaBot(sb.ToString(), kb);
    }

    private async Task<VistaBot> ActividadAsync(Torre t, int actId, int pag, bool soloPlanilla = false)
    {
        var volver = soloPlanilla ? $"t:{t.E.Id}:pll" : $"t:{t.E.Id}:act";
        var a = await _db.Actividades.AsNoTracking().FirstOrDefaultAsync(x => x.Id == actId && !x.Eliminado);
        if (a is null) return new VistaBot("Esa actividad ya no existe.", new() { Nav(t.E.Id, volver) });
        var ps = await _db.ParametrosActividad.AsNoTracking().Where(p => p.ActividadId == actId && p.Activo && !p.Eliminado && (!soloPlanilla || p.EnPlanilla))
            .OrderBy(p => p.Orden).ThenBy(p => p.Id).ToListAsync();
        var estados = await _db.EstadosParametro.AsNoTracking().Where(e => e.Activo).ToDictionaryAsync(e => e.Id);
        var sb = new StringBuilder(Ruta(t, soloPlanilla ? "Planillas" : "Actividades", a.Nombre));
        var kb = new List<List<Boton>>();
        var psIds = ps.Select(p => p.Id).ToList();

        string Celda(bool completado, int? estadoId, DateTime? fecha)
        {
            if (!completado) return "▫️ N/A";
            var e = estadoId is int id && estados.TryGetValue(id, out var es) ? es : null;
            var ic = e is null ? "🔴" : e.Porcentaje >= 100 ? "✅" : e.Porcentaje > 0 ? "⏳" : "🔴";
            return $"{ic} {H(e?.Nombre ?? "Sin estado")}" + (fecha is DateTime f ? $" · {f:dd/MM/yyyy}" : "");
        }

        var archivos = new List<ArchivoBot>();
        if (a.Ambito == AmbitoActividad.Tiro)
        {
            if (t.Tiro is null) sb.Append("\nLa torre no tiene tiro asignado.");
            else
            {
                var regs = await _db.RegistrosParametroTiro.AsNoTracking().Where(r => r.TiroId == t.Tiro.Id && psIds.Contains(r.ParametroActividadId)).ToListAsync();
                sb.Append($"\n<i>Tiro {H(t.Tiro.Nombre)}</i>\n");
                foreach (var p in ps)
                {
                    var r = regs.FirstOrDefault(x => x.ParametroActividadId == p.Id);
                    sb.Append($"• {H(p.Nombre)}: {(r is null ? "▫️ N/A" : Celda(r.Completado, r.EstadoParametroId, r.Fecha))}\n");
                }
            }
        }
        else
        {
            var hv = t.Comps.FirstOrDefault(c => c.EsHojaDeVida);
            var comps = a.Ambito == AmbitoActividad.Torre
                ? (hv is null ? new List<Componente>() : new List<Componente> { hv })
                : t.Comps.Where(c => !c.EsHojaDeVida).ToList();
            if (a.Ambito == AmbitoActividad.Pata && (soloPlanilla ||
                (a.TipoEnsayoId is null && !a.Nombre.StartsWith("Ensayos de ", StringComparison.OrdinalIgnoreCase))))
            {
                var ids = comps.Select(c => c.Id).ToList();
                var limit = (await _db.ComponenteActividades.AsNoTracking().Where(x => ids.Contains(x.ComponenteId)).ToListAsync())
                    .GroupBy(x => x.ComponenteId).ToDictionary(g => g.Key, g => g.Select(x => x.ActividadId).ToHashSet());
                comps = comps.Where(c => !limit.TryGetValue(c.Id, out var s) || s.Count == 0 || s.Contains(actId)).ToList();
            }
            var compIds = comps.Select(c => c.Id).ToList();
            var tiposEnsayo = await _db.TiposEnsayo.AsNoTracking().ToListAsync();
            var tipoEnsayo = TipoDeActividad(a, tiposEnsayo);
            if (!soloPlanilla && (a.TipoEnsayoId is not null || a.Nombre.StartsWith("Ensayos de ", StringComparison.OrdinalIgnoreCase)))
            {
                var cantidades = tipoEnsayo is null ? new Dictionary<int, int>() : await
                    (from vinculo in _db.ResultadosEnsayoComponente.AsNoTracking()
                     join resultado in _db.ResultadosEnsayo.AsNoTracking() on vinculo.ResultadoEnsayoId equals resultado.Id
                     where compIds.Contains(vinculo.ComponenteId) && resultado.TipoEnsayoId == tipoEnsayo.Id && !resultado.Eliminado
                     group resultado by vinculo.ComponenteId into g
                     select new { Id = g.Key, N = g.Select(r => r.Id).Distinct().Count() }).ToDictionaryAsync(x => x.Id, x => x.N);
                foreach (var c in comps)
                    sb.Append($"\n🧪 {H(c.Nombre)}: {cantidades.GetValueOrDefault(c.Id)} resultado(s)\n");
                kb.Add(Nav(t.E.Id, volver));
                return new VistaBot(sb.ToString(), kb);
            }
            var regs = await _db.RegistrosParametro.AsNoTracking().Where(r => compIds.Contains(r.ComponenteId) && psIds.Contains(r.ParametroActividadId)).ToListAsync();
            foreach (var c in comps)
            {
                if (a.Ambito == AmbitoActividad.Pata) sb.Append($"\n<b>{H(c.Nombre)}</b>\n"); else sb.Append('\n');
                foreach (var p in ps)
                {
                    if (sb.Length > 3300) break;
                    var r = regs.FirstOrDefault(x => x.ComponenteId == c.Id && x.ParametroActividadId == p.Id);
                    sb.Append($"• {H(Corto(p.Nombre, 50))}: {(r is null ? "▫️ N/A" : Celda(r.Completado, r.EstadoParametroId, r.Fecha))}\n");
                }
            }
            if (ps.Count == 0) sb.Append("\n<i>La actividad no tiene parámetros activos.</i>\n");
            var regIds = regs.Select(r => r.Id).ToList();
            var porReg = regs.ToDictionary(r => r.Id);
            var nombreP = ps.ToDictionary(p => p.Id, p => p.Nombre);
            var nombreC = comps.ToDictionary(c => c.Id, c => c.EsHojaDeVida ? "" : c.Nombre);
            archivos = (await _db.ArchivosRegistroParametro.AsNoTracking().Where(x => regIds.Contains(x.RegistroParametroId) && !x.Eliminado).OrderBy(x => x.Id).ToListAsync())
                .Select(x =>
                {
                    var r = porReg[x.RegistroParametroId];
                    var nota = string.Join(" · ", new[] { nombreC.GetValueOrDefault(r.ComponenteId), Corto(nombreP.GetValueOrDefault(r.ParametroActividadId), 25) }.Where(s => !string.IsNullOrEmpty(s)));
                    return new ArchivoBot('r', x.Id, x.NombreOriginal, nota);
                }).ToList();
        }
        Archivos(sb, kb, archivos, pag, $"t:{t.E.Id}:{(soloPlanilla ? "plc" : "ac")}:{actId}", soloPlanilla ? "Planillas adjuntas" : "Evidencias");
        if (soloPlanilla && archivos.Count == 0) sb.Append("\n<i>Aún no hay planillas adjuntas en esta torre.</i>");
        kb.Add(Nav(t.E.Id, volver));
        return new VistaBot(sb.ToString(), kb);
    }

    // ---------- Elementos (hoja de vida, patas, obras…) ----------
    private static VistaBot Elementos(Torre t)
    {
        var sb = new StringBuilder(Ruta(t, "Elementos"));
        sb.Append("\nCada elemento tiene su ficha con archivos, planos, materiales y actividades.\n");
        var kb = new List<List<Boton>>();
        var otros = t.Comps.Where(c => !c.EsHojaDeVida).Select(c => new Boton(Corto(c.Nombre, 20), $"t:{t.E.Id}:el:{c.Id}")).ToList();
        foreach (var fila in otros.Chunk(4)) kb.Add(fila.ToList());
        if (otros.Count == 0) sb.Append("<i>La torre no tiene elementos.</i>\n");
        kb.Add(Nav(t.E.Id, null));
        return new VistaBot(sb.ToString(), kb);
    }

    // Trazado.TipoEnsayoParaActividad: el enlace por Id manda; el nombre solo es respaldo legado.
    private static TipoEnsayo? TipoDeActividad(Actividad actividad, IEnumerable<TipoEnsayo> tipos)
    {
        if (actividad.TipoEnsayoId is int id) return tipos.FirstOrDefault(t => t.Id == id);
        const string prefijo = "Ensayos de ";
        if (!actividad.Nombre.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase)) return null;
        var nombre = actividad.Nombre[prefijo.Length..].Trim();
        return tipos.FirstOrDefault(t => nombre.Length > 0 && !string.IsNullOrWhiteSpace(t.Nombre)
            && (t.Nombre.Contains(nombre, StringComparison.OrdinalIgnoreCase)
                || nombre.Contains(t.Nombre, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<VistaBot> ElementoAsync(Torre t, int compId, int pag)
    {
        var c = t.Comps.FirstOrDefault(x => x.Id == compId);
        if (c is null) return new VistaBot("Ese elemento ya no existe.", new() { Nav(t.E.Id, $"t:{t.E.Id}:ele") });
        var sb = new StringBuilder(Ruta(t, "Elementos", c.Nombre));
        if (!string.IsNullOrWhiteSpace(c.InformacionAdicional)) sb.Append($"\n{H(Corto(c.InformacionAdicional, 400))}\n");

        // Actividades de pata que le aplican, con su % (misma regla de la matriz).
        var acts = (await ActividadesOrdenadasAsync()).Where(a => a.Ambito == AmbitoActividad.Pata).ToList();
        var limite = await _db.ComponenteActividades.AsNoTracking().Where(x => x.ComponenteId == compId).Select(x => x.ActividadId).ToListAsync();
        var resIds = await _db.ResultadosEnsayoComponente.AsNoTracking().Where(v => v.ComponenteId == compId).Select(v => v.ResultadoEnsayoId).Distinct().ToListAsync();
        var resultados = await _db.ResultadosEnsayo.AsNoTracking().Where(r => resIds.Contains(r.Id) && !r.Eliminado).ToListAsync();
        var tiposConDatos = resultados.Select(r => r.TipoEnsayoId).Distinct().ToList();
        var tiposElemento = await _db.TiposEnsayo.AsNoTracking().Where(t => tiposConDatos.Contains(t.Id)).ToListAsync();
        // Como la ficha de Trazado: seleccionadas expresamente o con resultados, aunque no estén marcadas.
        acts = acts.Where(a => limite.Contains(a.Id) || TipoDeActividad(a, tiposElemento) is not null).ToList();
        var actIds = acts.Select(a => a.Id).ToList();
        var ps = await _db.ParametrosActividad.AsNoTracking().Where(p => actIds.Contains(p.ActividadId) && p.Activo && !p.Eliminado).ToListAsync();
        var psIds = ps.Select(p => p.Id).ToList();
        var regs = await _db.RegistrosParametro.AsNoTracking().Where(r => r.ComponenteId == compId && psIds.Contains(r.ParametroActividadId)).ToListAsync();
        var pctEstado = await _db.EstadosParametro.AsNoTracking().Where(e => e.Activo).ToDictionaryAsync(e => e.Id, e => e.Porcentaje);
        var kb = new List<List<Boton>>();
        if (acts.Count > 0) sb.Append("\n<b>Actividades</b>\n");
        foreach (var a in acts)
        {
            if (a.TipoEnsayoId is not null || a.Nombre.StartsWith("Ensayos de ", StringComparison.OrdinalIgnoreCase))
            {
                var tipo = TipoDeActividad(a, tiposElemento);
                var n = tipo is null ? 0 : resultados.Count(r => r.TipoEnsayoId == tipo.Id);
                // La app muestra resultados de ensayo, no un % PIE: no inventar un 100 % por tener datos.
                sb.Append($"🧪 {H(a.Nombre)} · {n} resultado(s)\n");
                continue;
            }
            var celdas = ps.Where(p => p.ActividadId == a.Id).Select(p =>
            {
                var r = regs.FirstOrDefault(x => x.ParametroActividadId == p.Id);
                return r is null || !r.Completado ? (decimal?)null : (r.EstadoParametroId is int e && pctEstado.TryGetValue(e, out var v) ? v : 0);
            }).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            decimal? pct = CumplimientoPie.Resumir(celdas.Select(v => new CumplimientoPie.Celda(v, null, null))).Pct;
            var ic = pct is null ? "▫️" : pct >= 100 ? "✅" : pct > 0 ? "⏳" : "🔴";
            sb.Append($"{ic} {H(a.Nombre)} · {Pct(pct)}\n");
        }

        // Ensayos del elemento (ResultadoEnsayoComponente): por tipo, los últimos resultados con sus valores.
        if (resultados.Count > 0)
        {
            var tipoIds = resultados.Select(r => r.TipoEnsayoId).Distinct().ToList();
            var tipos = await _db.TiposEnsayo.AsNoTracking().Where(x => tipoIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
            var campos = (await _db.CamposEnsayo.AsNoTracking().Where(c => tipoIds.Contains(c.TipoEnsayoId) && c.Activo).ToListAsync())
                .GroupBy(c => c.TipoEnsayoId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Orden).ThenBy(c => c.Etiqueta).ToList());
            var valores = (await _db.ValoresCampoEnsayo.AsNoTracking().Where(v => resIds.Contains(v.ResultadoEnsayoId)).ToListAsync())
                .GroupBy(v => v.ResultadoEnsayoId).ToDictionary(g => g.Key, g => g.ToDictionary(v => v.CampoEnsayoId, v => v.Valor));
            var infIds = resultados.Where(r => r.InformeId != null).Select(r => r.InformeId!.Value).Distinct().ToList();
            var informes = await _db.Informes.AsNoTracking().Where(i => infIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.Numero);
            sb.Append($"\n<b>Ensayos</b> ({resultados.Count})\n");
            foreach (var g in resultados.GroupBy(r => r.TipoEnsayoId).OrderBy(g => tipos.TryGetValue(g.Key, out var tt) ? tt.Nombre : ""))
            {
                if (sb.Length > 3000) { sb.Append("…\n"); break; }
                var nombreTipo = tipos.TryGetValue(g.Key, out var tp) ? tp.Nombre : "Ensayo";
                sb.Append($"🧪 <b>{H(nombreTipo)}</b> · {g.Count()} resultado(s)\n");
                var cs = campos.GetValueOrDefault(g.Key) ?? new();
                // Campos marcados como Resumen en la tabla del ensayo (Ensayos › tipo › Campos; ResumenEnsayos).
                // Sin marcados: los 4 primeros con dato (lo de siempre).
                var mostrados = g.OrderByDescending(r => r.FechaEjecucion ?? r.FechaCreacion).Take(4).ToList();
                var resumenTabla = await SigWeb.Data.Ensayos.ResumenEnsayos.ParesAsync(_db, g.Key, mostrados);
                foreach (var r in mostrados)
                {
                    var vals = valores.GetValueOrDefault(r.Id) ?? new();
                    var partes = resumenTabla is not null
                        ? (resumenTabla.GetValueOrDefault(r.Id) ?? new()).Select(x => $"{Corto(x.Nombre, 24)}: {Corto(x.Valor, 24)}")
                        : cs.Where(c => vals.TryGetValue(c.Id, out var v) && !string.IsNullOrWhiteSpace(v)).Take(4)
                            .Select(c => $"{Corto(c.Etiqueta, 18)}: {Corto(Fecha(vals[c.Id]), 18)}{(string.IsNullOrWhiteSpace(c.Unidad) ? "" : " " + c.Unidad)}");
                    var cab = new[] { r.FechaEjecucion?.ToString("dd/MM/yyyy"), r.SubElemento != SubElementoResultado.Ninguno ? r.SubElemento.ToString() : null,
                        r.InformeId is int ii && informes.TryGetValue(ii, out var num) ? $"Inf. {num}" : null }.Where(x => x != null);
                    var cabecera = string.Join(" · ", cab.Where(x => !string.IsNullOrWhiteSpace(x)));
                    var resumen = string.Join(" · ", partes);
                    sb.Append($"   • {H(cabecera)}" + (resumen.Length > 0
                        ? (cabecera.Length > 0 ? "\n     " : "") + H(resumen)
                        : cabecera.Length == 0 ? "Sin datos de resumen" : "") + "\n");
                }
                if (g.Count() > 4) sb.Append($"   … y {g.Count() - 4} más\n");
            }
        }

        var archivos = new List<ArchivoBot>();
        archivos.AddRange(await ArchivosEnsayosAsync(resultados));
        archivos.AddRange(await OtrosDeAsync(new() { compId }));
        archivos.AddRange((await PlanosDeAsync(new() { compId })).Select(ArchivoPlano));
        archivos.AddRange(await AdjuntosMaterialesAsync(await MaterialesDeAsync(t, compId)));
        var regIds = regs.Select(r => r.Id).ToList();
        archivos.AddRange((await _db.ArchivosRegistroParametro.AsNoTracking().Where(x => regIds.Contains(x.RegistroParametroId) && !x.Eliminado).ToListAsync())
            .Select(x => new ArchivoBot('r', x.Id, x.NombreOriginal, "evidencia")));
        if (archivos.Count == 0) sb.Append("\n<i>Sin archivos, planos, ensayos ni materiales.</i>");
        Archivos(sb, kb, archivos, pag, $"t:{t.E.Id}:el:{compId}", "Informes, archivos, planos y materiales");
        var claveElemento = AmbitoCarpetasHojaVida.ClaveElemento(c.Nombre);
        var carpetas = await _db.CarpetasHojaVida.AsNoTracking().Where(x => x.Activo && x.ClaveElemento == claveElemento)
            .OrderBy(x => x.Orden).ThenBy(x => x.Id).ToListAsync();
        var conteos = await _db.ArchivosCarpetaHojaVida.AsNoTracking().Where(a => a.ElementoTrazadoId == t.E.Id && a.ComponenteId == compId)
            .GroupBy(a => a.CarpetaHojaVidaId).Select(g => new { Id = g.Key, Cantidad = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Cantidad);
        foreach (var carpeta in carpetas)
            kb.Add(new() { new($"📁 {Corto(carpeta.Nombre, 45)} ({conteos.GetValueOrDefault(carpeta.Id)})", $"t:{t.E.Id}:hc:{compId}:{carpeta.Id}:0") });
        kb.Add(Nav(t.E.Id, $"t:{t.E.Id}:ele"));
        return new VistaBot(sb.ToString(), kb);
    }
}
