using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using ProyectoDiseño.Patrones;
using ProyectoDiseño.Models;
using System;
using System.Collections.Generic;

namespace ProyectoDiseño.Controllers
{
    public class InsumosController : Controller
    {
        // GET: Insumos (Catálogo General)
        [HttpGet]
        public IActionResult Index()
        {
            var lista = new List<Insumo>();
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            try
            {
                using (conexion)
                {
                    conexion.Open();
                    string query = "SELECT IdInsumo, Nombre, UnidadMedida, CantidadActual, StockMinimo FROM Insumo WHERE Activo = 1";
                    using (SqlCommand cmd = new SqlCommand(query, conexion))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new Insumo
                                {
                                    IdInsumo = reader.GetInt32(0),
                                    Nombre = reader.GetString(1),
                                    UnidadMedida = reader.GetString(2),
                                    CantidadActual = reader.GetDecimal(3),
                                    StockMinimo = reader.GetDecimal(4)
                                });
                            }
                        }
                    }
                }
                return View(lista);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cargar el catálogo: " + ex.Message;
                return RedirectToAction("Dashboard", "Home");
            }
        }

        // CORREGIDO - POST: Insumos/ClonarInsumo (Uso del Patrón Prototype)
        [HttpPost]
        public IActionResult ClonarInsumo(int idInsumoBase, string nuevoNombre, decimal nuevaCantidad)
        {
            try
            {
                // Se obtiene el insumo real de la base de datos para usarlo como prototipo de plantilla
                Insumo insumoBase = ObtenerInsumoPorId(idInsumoBase);
                if (insumoBase == null) return NotFound();

                // Clonación del objeto base heredando UnidadMedida y StockMinimo automáticamente
                Insumo nuevoInsumo = insumoBase.Clonar(nuevoNombre, nuevaCantidad);

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

                TempData["Exito"] = "Insumo clonado correctamente mediante el Patrón Prototype.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al clonar el registro: " + ex.Message;
                return RedirectToAction("Index");
            }
        }

        // CORREGIDO: Consulta real con ADO.NET utilizando la conexión centralizada del Singleton
        private Insumo ObtenerInsumoPorId(int id)
        {
            Insumo insumo = null;
            SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();

            using (conexion)
            {
                conexion.Open();
                string query = "SELECT IdInsumo, Nombre, UnidadMedida, CantidadActual, StockMinimo, Activo FROM Insumo WHERE IdInsumo = @IdInsumo";
                using (SqlCommand cmd = new SqlCommand(query, conexion))
                {
                    cmd.Parameters.AddWithValue("@IdInsumo", id);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            insumo = new Insumo
                            {
                                IdInsumo = reader.GetInt32(0),
                                Nombre = reader.GetString(1),
                                UnidadMedida = reader.GetString(2),
                                CantidadActual = reader.GetDecimal(3),
                                StockMinimo = reader.GetDecimal(4),
                                Activo = reader.GetBoolean(5)
                            };
                        }
                    }
                }
            }
            return insumo;
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
            if (insumo.StockMinimo <= 0)
            {
                ModelState.AddModelError("StockMinimo", "El stock de seguridad mínimo debe ser un valor numérico superior a cero.");
                return View(insumo);
            }

            if (ModelState.IsValid)
            {
                try
                {
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

                            cmd.ExecuteNonQuery();
                        }
                    }
                    return RedirectToAction("Index");
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", "Error al guardar en la base de datos: " + ex.Message);
                }
            }
            return View(insumo);
        }

        // GET: Insumos/Editar/5
        [HttpGet]
        public IActionResult Editar(int id)
        {
            Insumo insumo = ObtenerInsumoPorId(id);
            if (insumo == null) return NotFound();
            return View(insumo);
        }

        // POST: Insumos/Editar
        [HttpPost]
        public IActionResult Editar(Insumo insumo)
        {
            if (insumo.StockMinimo <= 0)
            {
                ModelState.AddModelError("StockMinimo", "El stock mínimo debe ser mayor a cero.");
                return View(insumo);
            }

            try
            {
                SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();
                using (conexion)
                {
                    conexion.Open();
                    string query = @"UPDATE Insumo SET Nombre = @Nombre, UnidadMedida = @UnidadMedida, 
                                     StockMinimo = @StockMinimo, Activo = @Activo WHERE IdInsumo = @IdInsumo";
                    using (SqlCommand cmd = new SqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@Nombre", insumo.Nombre);
                        cmd.Parameters.AddWithValue("@UnidadMedida", insumo.UnidadMedida);
                        cmd.Parameters.AddWithValue("@StockMinimo", insumo.StockMinimo);
                        cmd.Parameters.AddWithValue("@Activo", insumo.Activo);
                        cmd.Parameters.AddWithValue("@IdInsumo", insumo.IdInsumo);
                        cmd.ExecuteNonQuery();
                    }
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error interno al actualizar el registro: " + ex.Message);
                return View(insumo);
            }
        }

        // POST: Insumos/Eliminar (Baja Lógica CRUD - RF-01)
        [HttpPost]
        public IActionResult Eliminar(int idInsumo)
        {
            try
            {
                SqlConnection conexion = DatabaseConnection.Instancia.ObtenerConexion();
                using (conexion)
                {
                    conexion.Open();
                    string query = "UPDATE Insumo SET Activo = 0 WHERE IdInsumo = @IdInsumo";
                    using (SqlCommand cmd = new SqlCommand(query, conexion))
                    {
                        cmd.Parameters.AddWithValue("@IdInsumo", idInsumo);
                        cmd.ExecuteNonQuery();
                    }
                }
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al dar de baja el insumo: " + ex.Message;
                return RedirectToAction("Index");
            }
        }
    }
}