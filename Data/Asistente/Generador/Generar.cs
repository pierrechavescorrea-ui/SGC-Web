#if GENERADOR_INDICE
using System.Text.Json;
using SigWeb.Data.Asistente;

var controles = LectorInterfaz.Leer(args[0]);
File.WriteAllText(args[1], JsonSerializer.Serialize(controles));
#endif
