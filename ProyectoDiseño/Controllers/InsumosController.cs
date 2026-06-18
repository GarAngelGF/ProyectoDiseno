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
        // ==========================================
        // AGREGADO: GET: Insumos (Catálogo)
        // ==========================================
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
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Insumos/ClonarInsumo
        [HttpPost]
        public IActionResult ClonarInsumo(int idInsumoBase, string nuevoNombre, decimal nuevaCantidad)
        {
            try
            {
                Insumo insumoBase = ObtenerInsumoPorId(idInsumoBase);
                if (insumoBase == null) return NotFound();

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

                return RedirectToAction("Index"); // Ahora sí funcionará
            }
            catch (Exception ex)
            {
                return View("Error");
            }
        }

        private Insumo ObtenerInsumoPorId(int id)
        {
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
                    return RedirectToAction("Index"); // Ahora sí funcionará
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