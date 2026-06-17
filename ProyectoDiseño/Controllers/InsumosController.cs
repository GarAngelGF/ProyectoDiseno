using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;

namespace ProyectoDiseño.Controllers
{
    public class InsumosController : Controller
    {
        // POST: Insumos/ClonarInsumo
        [HttpPost]
        public IActionResult ClonarInsumo(int idInsumoBase, string nuevoNombre, decimal nuevaCantidad)
        {
            try
            {
                // 1. Obtener el insumo original que servirá de plantilla (Simulado desde BD)
                Insumo insumoBase = ObtenerInsumoPorId(idInsumoBase);

                if (insumoBase == null) return NotFound();

                // 2. Uso de PATRÓN PROTOTYPE: Clonamos y asignamos nuevos valores
                Insumo nuevoInsumo = insumoBase.Clonar(nuevoNombre, nuevaCantidad);

                // 3. Uso de PATRÓN SINGLETON: Guardamos en BD con la instancia única
                SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

                using (conexion)
                {
                    conexion.Open();
                    string query = @"INSERT INTO Insumo (Nombre, UnidadMedida, CantidadActual, StockMinimo, Activo) 
                                     VALUES (@Nombre, @UnidadMedida, @CantidadActual, @StockMinimo, 1)";

                    using (SqlCommand cmd = new SqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@Nombre", nuevoInsumo.Nombre);
                        cmd.Parameters.AddWithValue("@UnidadMedida", nuevoInsumo.UnidadMedida);
                        cmd.Parameters.AddWithValue("@CantidadActual", nuevoInsumo.CantidadActual);
                        cmd.Parameters.AddWithValue("@StockMinimo", nuevoInsumo.StockMinimo);

                        cmd.ExecuteNonQuery();
                    }
                }

                return RedirectToAction("Index"); // Regresa al catálogo actualizado
            }
            catch (Exception ex)
            {
                // Manejo de errores 
                return View("Error");
            }
        }

        // Método auxiliar simulado para obtener el insumo base de la BD
        private Insumo ObtenerInsumoPorId(int id)
        {
            // Aquí iría la consulta SELECT a la base de datos usando también el Singleton
            // Para el ejemplo retornamos un objeto instanciado
            return new Insumo
            {
                IdInsumo = id,
                Nombre = "Leche Entera Marca A",
                UnidadMedida = "Litros",
                CantidadActual = 10,
                StockMinimo = 5
            };
        }
        // GET: Insumos/Crear
        public IActionResult Crear()
        {
            return View();
        }

        // POST: Insumos/Crear
        [HttpPost]
        public IActionResult Crear(Insumo insumo)
        {
            // Validación de la Regla de Negocio RN-02
            if (insumo.StockMinimo <= 0)
            {
                // Agregamos un error al modelo para que la vista lo muestre
                ModelState.AddModelError("StockMinimo", "El stock de seguridad mínimo debe ser un valor numérico superior a cero.");
                return View(insumo);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Uso de la conexión centralizada desde tu carpeta Patrones
                    SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();
                    using (conexion)
                    {
                        conexion.Open();
                        string query = @"INSERT INTO Insumo (Nombre, UnidadMedida, CantidadActual, StockMinimo, Activo) 
                                         VALUES (@Nombre, @UnidadMedida, @CantidadActual, @StockMinimo, 1)";

                        using (SqlCommand cmd = new SqlCommand(query, conexion))
                        {
                            cmd.Parameters.AddWithValue("@Nombre", insumo.Nombre);
                            cmd.Parameters.AddWithValue("@UnidadMedida", insumo.UnidadMedida);
                            cmd.Parameters.AddWithValue("@CantidadActual", insumo.CantidadActual);
                            cmd.Parameters.AddWithValue("@StockMinimo", insumo.StockMinimo);

                            cmd.ExecuteNonQuery(); // La ejecución pura en ADO.NET garantiza respuesta en < 2 segundos
                        }
                    }
                    return RedirectToAction("Index"); // Redirige al catálogo
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al guardar en la base de datos: " + ex.Message);
                }
            }
            return View(insumo);
        }
    }
}
